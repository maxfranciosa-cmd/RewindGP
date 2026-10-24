using AMS2ChEd.Business.Services.CrashDetection;

namespace AMS2ChEd.Tests
{
    [TestClass]
    public class CrashDetectorTests
    {
        private const double Dt = 0.05;
        private const double RaceSpeed = 60; // 216 km/h

        // A race at 20 Hz: three cars on a straight, 100 m apart, the crash car ("B") in the middle.
        // Each step function gets the frame number and edits the cars before the frame is taken.
        private class Scenario
        {
            public List<TelemetryFrame> Frames { get; } = new();
            private readonly Dictionary<string, TelemetryCarSample> _cars;
            private double _time;

            public Scenario()
            {
                _cars = new[] { "A", "B", "C" }
                    .Select((name, i) => new TelemetryCarSample
                    {
                        Index = i,
                        Name = name,
                        RaceState = TelemetryRaceState.Racing,
                        Speed = RaceSpeed,
                        X = 1000 - i * 100,
                        LapDistance = 1000 - i * 100,
                        CurrentLap = 3
                    })
                    .ToDictionary(c => c.Name);
            }

            public TelemetryCarSample this[string name] => _cars[name];

            // frozen: the game stalls, every frame repeats the last one's cars
            public Scenario Run(int frames, Action<int> step = null, TelemetryGameState gameState = TelemetryGameState.Playing, int viewed = 0,
                TelemetrySessionState session = TelemetrySessionState.Race, bool frozen = false)
            {
                for (int i = 0; i < frames; i++)
                {
                    step?.Invoke(i);
                    if (gameState == TelemetryGameState.Playing && !frozen)
                    {
                        foreach (var car in _cars.Values)
                        {
                            car.X += car.Speed * Dt;
                            car.LapDistance = car.X % 4000;
                        }
                    }

                    _time += Dt;
                    Frames.Add(new TelemetryFrame
                    {
                        Time = _time,
                        GameState = gameState,
                        SessionState = session,
                        ViewedCarIndex = viewed,
                        Cars = _cars.Values.Select(Copy).ToList()
                    });
                }
                return this;
            }

            // B brakes to this speed at 1 m/s per sample (2 g), nothing a crash
            public Scenario SlowTo(double speed)
            {
                while (this["B"].Speed > speed)
                {
                    this["B"].Speed = Math.Max(speed, this["B"].Speed - 1);
                    Run(1);
                }
                return this;
            }

            // B hits a wall: from the impact speed to a standstill in two samples, then sits there
            public Scenario Crash(double impactSpeed = RaceSpeed)
            {
                SlowTo(impactSpeed);
                this["B"].Speed = impactSpeed / 3;
                Run(1);
                this["B"].Speed = 0;
                return this;
            }

            // B hits something at 144 km/h, the speed gone in a third of a second (~12 g): Moderate
            public Scenario MediumCrash()
            {
                SlowTo(40);
                return Run(7, i => this["B"].Speed = Math.Max(0, 40 - (i + 1) * 6.0));
            }

            private static TelemetryCarSample Copy(TelemetryCarSample c) => new()
            {
                Index = c.Index, Name = c.Name, RaceState = c.RaceState, PitState = c.PitState, Speed = c.Speed,
                Pitch = c.Pitch, Yaw = c.Yaw, Roll = c.Roll, X = c.X, Y = c.Y, Z = c.Z, LapDistance = c.LapDistance,
                CurrentLap = c.CurrentLap, YellowFlag = c.YellowFlag, FlagReason = c.FlagReason, AccelerationG = c.AccelerationG,
                CrashState = c.CrashState, WallContact = c.WallContact, WheelsDetached = c.WheelsDetached
            };
        }

        // the frames, then the race's end
        private static List<CrashIncident> Detect(IEnumerable<TelemetryFrame> frames, CrashDetectionThresholds thresholds = null)
        {
            var detector = new CrashDetector(thresholds);
            foreach (var frame in frames)
                detector.Process(frame);
            detector.Flush();
            return detector.Incidents.ToList();
        }

        // the frames only: the race still going
        private static List<CrashIncident> DetectSoFar(IEnumerable<TelemetryFrame> frames)
        {
            var detector = new CrashDetector();
            foreach (var frame in frames)
                detector.Process(frame);
            return detector.Incidents.ToList();
        }

        // every candidate a crash, whatever the car did next: what makes one
        private static readonly CrashDetectionThresholds EveryTrigger = new()
        {
            StoppedSpeed = double.MaxValue,
            MinStoppedTime = 0,
            MaxTimeToStop = double.MaxValue,
            DriveOnDistance = double.MaxValue
        };

        // a minute and a bit of play: longer than the confirmation window
        private const int PastTheWindow = 1300;

        [TestMethod]
        public void CleanRacing_FindsNothing()
        {
            var scenario = new Scenario();
            scenario.Run(800, i =>
            {
                // braking for a corner every four seconds: 60 to 25 m/s in a second (~3.6 g), then
                // back up to speed
                var phase = i % 80;
                foreach (var name in new[] { "A", "B", "C" })
                    scenario[name].Speed = phase < 20 ? RaceSpeed - phase * 1.75 : phase < 40 ? 25 + (phase - 20) * 1.75 : RaceSpeed;
            });

            Assert.AreEqual(0, Detect(scenario.Frames, EveryTrigger).Count);
        }

        [TestMethod]
        public void HeavyBraking_IsNotACrash()
        {
            // 2 m/s per sample, ~4 g: the hardest braking recorded is around 6.5 g
            var scenario = new Scenario();
            scenario.Run(40).Run(20, i => scenario["B"].Speed = Math.Max(20, RaceSpeed - (i + 1) * 2.0)).Run(100);

            Assert.AreEqual(0, Detect(scenario.Frames, EveryTrigger).Count);
        }

        [TestMethod]
        public void HighSpeedImpact_LeavingTheCarStranded_IsASevereCrash()
        {
            var scenario = new Scenario().Run(40);
            scenario.Crash().Run(100);

            var incident = Detect(scenario.Frames).Single();

            Assert.AreEqual("B", incident.CarName);
            Assert.AreEqual(CrashSeverity.Severe, incident.Severity);
            Assert.AreEqual(RaceSpeed, incident.ImpactSpeed, 0.001);
            Assert.IsFalse(incident.Retired);
        }

        [TestMethod]
        public void StrandedCar_IsConfirmedOnceTheWindowRunsOut()
        {
            var scenario = new Scenario().Run(40);
            scenario.Crash().Run(100);

            // still within the window: it might yet drive on
            Assert.AreEqual(0, DetectSoFar(scenario.Frames).Count);

            scenario.Run(PastTheWindow);
            Assert.AreEqual(1, DetectSoFar(scenario.Frames).Count);
        }

        [TestMethod]
        public void Impact_ThenDrivingOn_IsNotSerious()
        {
            var scenario = new Scenario().Run(40);
            scenario.MediumCrash().Run(10).Run(100, i => scenario["B"].Speed = Math.Min(40, i * 2.0));

            Assert.AreEqual(0, Detect(scenario.Frames).Count);
        }

        [TestMethod]
        public void SevereImpact_ThenDrivingOn_IsReportedAsDrivenOn()
        {
            var scenario = new Scenario().Run(40);
            scenario.Crash().Run(10).Run(100, i => scenario["B"].Speed = Math.Min(40, i * 2.0));

            // known as soon as the car is away, without waiting for the window
            var incident = DetectSoFar(scenario.Frames).Single();
            Assert.IsTrue(incident.DroveOn);
            Assert.AreEqual(CrashSeverity.Severe, incident.Severity);
        }

        [TestMethod]
        public void DrivenOnCrash_ThenStrandedByAnother_IsReplacedByIt()
        {
            var scenario = new Scenario().Run(40);
            scenario.Crash().Run(10).Run(100, i => scenario["B"].Speed = Math.Min(RaceSpeed, i * 2.0));
            scenario.Run(40, _ => scenario["B"].Speed = RaceSpeed).Crash().Run(PastTheWindow);

            var incident = Detect(scenario.Frames).Single();
            Assert.IsFalse(incident.DroveOn);
        }

        [TestMethod]
        public void TwoCrashesDrivenOn_AreReportedOnce()
        {
            var scenario = new Scenario().Run(40);
            scenario.Crash().Run(10).Run(100, i => scenario["B"].Speed = Math.Min(RaceSpeed, i * 2.0));
            scenario.Run(40, _ => scenario["B"].Speed = RaceSpeed).Crash().Run(10).Run(100, i => scenario["B"].Speed = Math.Min(40, i * 2.0));

            Assert.AreEqual(1, Detect(scenario.Frames).Count);
        }

        [TestMethod]
        public void StuckInAPileUp_ThenDrivingOn_IsNotSerious()
        {
            // stopped for half a minute, blocked in, then away
            var scenario = new Scenario().Run(40);
            scenario.MediumCrash().Run(600).Run(100, _ => scenario["B"].Speed = 20).Run(PastTheWindow);

            Assert.AreEqual(0, Detect(scenario.Frames).Count);
        }

        [TestMethod]
        public void StrandedCar_ShufflingAFewMetres_StillCounts()
        {
            // trying to get free: a metre or so at a time, now and then
            var scenario = new Scenario().Run(40);
            scenario.Crash().Run(PastTheWindow, i => scenario["B"].Speed = i % 100 < 10 ? 2 : 0);

            Assert.AreEqual(1, Detect(scenario.Frames).Count);
        }

        [TestMethod]
        public void Impact_NeverComingToAStop_IsNotSerious()
        {
            // limping round at walking pace for the rest of the window
            var scenario = new Scenario().Run(40);
            scenario.MediumCrash().Run(PastTheWindow, _ => scenario["B"].Speed = 5);

            Assert.AreEqual(0, Detect(scenario.Frames).Count);
        }

        [TestMethod]
        public void Impact_ThenRetiring_CountsAtOnce()
        {
            var scenario = new Scenario().Run(40);
            scenario.Crash().Run(6).Run(10, _ => scenario["B"].RaceState = TelemetryRaceState.Retired);

            var incident = DetectSoFar(scenario.Frames).Single();
            Assert.IsTrue(incident.Retired);
        }

        [TestMethod]
        public void Impact_ThenSentBackToTheGarage_CountsAsRetiring()
        {
            // the player retiring from the menu: the car is back in the garage, far away
            var scenario = new Scenario().Run(40);
            scenario.Crash().Run(100).Run(10, _ =>
            {
                scenario["B"].X = -5000;
                scenario["B"].PitState = TelemetryPitState.InGarage;
            });

            Assert.IsTrue(DetectSoFar(scenario.Frames).Single().Retired);
        }

        [TestMethod]
        public void SlowImpact_IsIgnored()
        {
            var scenario = new Scenario().Run(40);
            scenario.Crash(impactSpeed: 20).Run(100);

            Assert.AreEqual(0, Detect(scenario.Frames).Count);
        }

        [TestMethod]
        public void MediumImpact_IsModerate()
        {
            // a hit at 144 km/h, the speed gone in a third of a second (~12 g)
            var scenario = new Scenario().Run(40).SlowTo(40);
            scenario.Run(10, i => scenario["B"].Speed = Math.Max(0, 40 - (i + 1) * 6.0)).Run(100);

            Assert.AreEqual(CrashSeverity.Moderate, Detect(scenario.Frames).Single().Severity);
        }

        [TestMethod]
        public void ReturnToGarage_TeleportIsNotAnImpact()
        {
            var scenario = new Scenario().Run(40);
            // the car vanishes to the garage: position jumps, speed drops to zero in one sample
            scenario.Run(1, _ =>
            {
                scenario["B"].Speed = 0;
                scenario["B"].X = -5000;
                scenario["B"].PitState = TelemetryPitState.InGarage;
            }).Run(100);

            Assert.AreEqual(0, Detect(scenario.Frames, EveryTrigger).Count);
        }

        [TestMethod]
        public void GameStall_IsNotAnImpact()
        {
            // the game stalls for half a second though its page repeats for only three samples,
            // then B moves on 25 m at once, slower than before: over the clock's 0.2 s that reads
            // as 15 g
            var scenario = new Scenario().Run(40);
            scenario.Run(3, frozen: true);
            scenario.Run(1, _ =>
            {
                scenario["B"].Speed = 30;
                scenario["B"].X += 25;
            });
            scenario.Run(100);

            Assert.AreEqual(0, Detect(scenario.Frames, EveryTrigger).Count);
        }

        [TestMethod]
        public void Spin_IsNotAnUpset()
        {
            var scenario = new Scenario().Run(40);
            scenario.Run(40, i =>
            {
                scenario["B"].Yaw = (i * 20) % 360 - 180;
                scenario["B"].Speed = Math.Max(0, RaceSpeed - i * 1.5);
            }).Run(100);

            Assert.AreEqual(0, Detect(scenario.Frames, EveryTrigger).Count);
        }

        [TestMethod]
        public void Rollover_AtSpeed_IsASevereCrash()
        {
            // at 40 m/s, under the severe impact speed: the roll is what makes it severe
            var scenario = new Scenario().Run(40).SlowTo(40);
            scenario.Run(10, i =>
            {
                scenario["B"].Roll = Math.Min(180, (i + 1) * 30);
                scenario["B"].Speed = Math.Max(0, 40 - (i + 1) * 6.0);
            }).Run(100);

            var incident = Detect(scenario.Frames).Single();
            Assert.AreEqual(CrashSeverity.Severe, incident.Severity);
            Assert.AreEqual(180, incident.MaxUpsetAngle, 0.001);
        }

        [TestMethod]
        public void CrashSeenInAReplay_IsIgnored()
        {
            var live = new Scenario().Run(40);
            live.Run(100);

            var replay = new Scenario();
            replay.Run(40, gameState: TelemetryGameState.Replay);
            replay["B"].Speed = 0;
            replay.Run(100, gameState: TelemetryGameState.Replay);

            Assert.AreEqual(0, Detect(live.Frames.Concat(replay.Frames)).Count);
        }

        [TestMethod]
        public void SpeedLostAcrossAPause_IsNotMeasured()
        {
            var scenario = new Scenario().Run(40);
            scenario.Run(200, gameState: TelemetryGameState.Paused);
            // back from the menu at a standstill (e.g. the car was reset meanwhile)
            scenario["B"].Speed = 0;
            scenario.Run(100);

            Assert.AreEqual(0, Detect(scenario.Frames).Count);
        }

        [TestMethod]
        public void BackFromAReplay_TheCarsSettlingIsNotACrash()
        {
            // the first samples after a replay: B's angles still swinging back from the replay's,
            // its speed dropping to what it really was
            var scenario = new Scenario().Run(40);
            scenario.Run(100, gameState: TelemetryGameState.Replay);
            scenario.Run(10, i =>
            {
                scenario["B"].Roll = 100 - i * 10;
                scenario["B"].Speed = i == 0 ? RaceSpeed : 10;
            });
            scenario.Run(100);

            Assert.AreEqual(0, Detect(scenario.Frames, EveryTrigger).Count);
        }

        [TestMethod]
        public void PausingDuringTheConfirmationWindow_DoesNotRunItDown()
        {
            var scenario = new Scenario().Run(40);
            scenario.Crash().Run(4);
            // two minutes behind the menu
            scenario.Run(2400, gameState: TelemetryGameState.Paused);
            scenario.Run(100);

            Assert.AreEqual(0, DetectSoFar(scenario.Frames).Count);

            scenario.Run(PastTheWindow);
            Assert.AreEqual(1, DetectSoFar(scenario.Frames).Count);
        }

        [TestMethod]
        public void Restart_ForgetsTheRaceSoFar()
        {
            var scenario = new Scenario().Run(40);
            scenario.Crash().Run(100);
            scenario.Run(1, gameState: TelemetryGameState.Restarting);

            Assert.AreEqual(0, Detect(scenario.Frames).Count);
        }

        [TestMethod]
        public void RestartThroughAFormationLap_ForgetsTheRaceSoFar()
        {
            var scenario = new Scenario().Run(40);
            scenario.Crash().Run(100);
            // no restarting state seen: straight into a new formation lap
            scenario.Run(40, session: TelemetrySessionState.FormationLap);

            Assert.AreEqual(0, Detect(scenario.Frames).Count);
        }

        [TestMethod]
        public void RestartBackOnTheGrid_ForgetsTheRaceSoFar()
        {
            var scenario = new Scenario().Run(40);
            scenario.Crash().Run(PastTheWindow);
            // a standing start without a formation lap: every car waiting on the grid again
            scenario.Run(20, _ =>
            {
                foreach (var name in new[] { "A", "B", "C" })
                {
                    scenario[name].RaceState = TelemetryRaceState.NotStarted;
                    scenario[name].Speed = 0;
                }
            });

            Assert.AreEqual(0, Detect(scenario.Frames).Count);
        }

        [TestMethod]
        public void CrashAfterARestart_StillCounts()
        {
            var scenario = new Scenario().Run(40);
            scenario.Crash().Run(100);
            scenario.Run(40, session: TelemetrySessionState.FormationLap);
            // the restarted race: back in grid order, B up to speed again, then crashing again
            scenario.Run(40, i =>
            {
                if (i == 0)
                {
                    foreach (var (name, x) in new[] { ("A", 1000.0), ("B", 900.0), ("C", 800.0) })
                    {
                        scenario[name].X = x;
                        scenario[name].LapDistance = x;
                    }
                }
                scenario["B"].Speed = RaceSpeed;
            }, session: TelemetrySessionState.FormationLap);
            scenario.Run(40);
            scenario.Crash().Run(100);

            Assert.AreEqual("B", Detect(scenario.Frames).Single().CarName);
        }

        [TestMethod]
        public void OneCarStillOnTheGrid_IsNotARestart()
        {
            var scenario = new Scenario().Run(40);
            scenario.Crash().Run(100);
            // A never got away from the start, the others are racing
            scenario.Run(20, _ => scenario["A"].RaceState = TelemetryRaceState.NotStarted);

            Assert.AreEqual(1, Detect(scenario.Frames).Count);
        }

        [TestMethod]
        public void LeavingTheRaceSession_EndsTheWindows()
        {
            // the race over (the results screen) with B still stranded: it counts
            var scenario = new Scenario().Run(40);
            scenario.Crash().Run(100);
            scenario.Run(1, gameState: TelemetryGameState.Other, session: TelemetrySessionState.Other);

            Assert.AreEqual(1, DetectSoFar(scenario.Frames).Count);
        }

        [TestMethod]
        public void ImpactInThePitLane_IsIgnored()
        {
            var scenario = new Scenario().Run(40);
            scenario["B"].PitState = TelemetryPitState.DrivingIntoPits;
            scenario.Crash().Run(100);

            Assert.AreEqual(0, Detect(scenario.Frames).Count);
        }

        [TestMethod]
        public void Impact_ThenDrivingIntoThePits_IsNotSerious()
        {
            var scenario = new Scenario().Run(40);
            scenario.MediumCrash().Run(40).Run(100, _ =>
            {
                scenario["B"].Speed = 15;
                scenario["B"].PitState = TelemetryPitState.DrivingIntoPits;
            });

            Assert.AreEqual(0, Detect(scenario.Frames).Count);
        }

        [TestMethod]
        public void AfterTheChequeredFlag_NothingCounts()
        {
            var scenario = new Scenario().Run(40);
            scenario["B"].RaceState = TelemetryRaceState.Finished;
            scenario.Crash().Run(100);

            Assert.AreEqual(0, Detect(scenario.Frames).Count);
        }

        [TestMethod]
        public void ACarIsReportedOnceAtMost()
        {
            var scenario = new Scenario().Run(40);
            scenario.Crash().Run(PastTheWindow);
            scenario["B"].Speed = RaceSpeed;
            scenario.Run(20).Crash().Run(100);

            Assert.AreEqual(1, Detect(scenario.Frames).Count);
        }

        [TestMethod]
        public void ViewedCar_RealGForce_MakesItSevere()
        {
            // the medium impact (Moderate on its speed), with the car's own g-force known
            var scenario = new Scenario().Run(40).SlowTo(40);
            scenario.Run(10, i =>
            {
                scenario["B"].Speed = Math.Max(0, 40 - (i + 1) * 6.0);
                scenario["B"].AccelerationG = i == 0 ? 55 : 1;
            }, viewed: 1).Run(100, viewed: 1);

            var incident = Detect(scenario.Frames).Single();
            Assert.IsTrue(incident.WasViewedCar);
            Assert.AreEqual(CrashSeverity.Severe, incident.Severity);
        }

        [TestMethod]
        public void ViewedCar_Rolling_IsATrigger()
        {
            // a roll the averaged speed hardly shows, short of the upset angle, at 180 km/h; then
            // carrying on: a Severe crash driven on from
            var scenario = new Scenario().Run(40).SlowTo(50);
            scenario.Run(5, _ => scenario["B"].CrashState = TelemetryCrashState.Rolling, viewed: 1);
            scenario.Run(100, _ => scenario["B"].CrashState = TelemetryCrashState.None, viewed: 1);

            var incident = Detect(scenario.Frames).Single();
            Assert.IsTrue(incident.DroveOn);
            Assert.IsTrue(incident.Rolled);
        }

        [TestMethod]
        public void ViewedCar_HardHit_IsATrigger()
        {
            // 25 g on the car's own sensors, then a gentle stop where it stays
            var scenario = new Scenario().Run(40).SlowTo(40);
            scenario.Run(1, _ => scenario["B"].AccelerationG = 25, viewed: 1);
            scenario.Run(40, _ =>
            {
                scenario["B"].AccelerationG = 1;
                scenario["B"].Speed = Math.Max(0, scenario["B"].Speed - 1);
            }, viewed: 1).Run(100, viewed: 1);

            Assert.AreEqual(1, Detect(scenario.Frames).Count);
        }

        [TestMethod]
        public void OtherCars_GForce_IsNotATrigger()
        {
            // the g-force and crash state are the viewed car's: on any other they mean nothing
            var scenario = new Scenario().Run(40).SlowTo(40);
            scenario.Run(1, _ => scenario["B"].AccelerationG = 25, viewed: 0);
            scenario.Run(40, _ => scenario["B"].Speed = Math.Max(0, scenario["B"].Speed - 1)).Run(100);

            Assert.AreEqual(0, Detect(scenario.Frames).Count);
        }

        [TestMethod]
        public void Csv_RoundTrip_GivesTheSameIncidents()
        {
            var scenario = new Scenario().Run(40);
            scenario.Crash().Run(100);

            var writer = new StringWriter();
            TelemetryCsv.WriteHeader(writer);
            for (int i = 0; i < scenario.Frames.Count; i++)
                TelemetryCsv.WriteFrame(writer, i, scenario.Frames[i]);

            var frames = TelemetryCsv.Read(new StringReader(writer.ToString()));

            Assert.AreEqual(scenario.Frames.Count, frames.Count);
            Assert.AreEqual(3, frames[0].Cars.Count);
            Assert.AreEqual("B", Detect(frames).Single().CarName);
        }

        [TestMethod]
        public void Csv_KeepsAFrameWithNoCars_AndQuotedNames()
        {
            var writer = new StringWriter();
            TelemetryCsv.WriteHeader(writer);
            TelemetryCsv.WriteFrame(writer, 0, new TelemetryFrame { Time = 1, GameState = TelemetryGameState.Other });
            TelemetryCsv.WriteFrame(writer, 1, new TelemetryFrame
            {
                Time = 2,
                Cars = { new TelemetryCarSample { Name = "Smith, \"Jr\"", AccelerationG = 1.5, CrashState = TelemetryCrashState.Rolling, WallContact = true } }
            });

            var frames = TelemetryCsv.Read(new StringReader(writer.ToString()));

            Assert.AreEqual(2, frames.Count);
            Assert.AreEqual(0, frames[0].Cars.Count);
            var car = frames[1].Cars.Single();
            Assert.AreEqual("Smith, \"Jr\"", car.Name);
            Assert.AreEqual(TelemetryCrashState.Rolling, car.CrashState);
            Assert.IsTrue(car.WallContact);
            Assert.IsNull(car.AeroDamage);
        }    }
}
