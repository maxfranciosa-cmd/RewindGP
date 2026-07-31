namespace AMS2ChEd.Business.Storage.Contracts
{
    public class SeasonModInstallResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int SeasonYear { get; set; }
        public bool IsUpdate { get; set; }
        public Exception Exception { get; set; }
        public string CleanupWarning { get; set; }

        public List<string> CopiedFolders { get; set; } = new List<string>();
        public List<string> CopiedFiles { get; set; } = new List<string>();
        public List<string> OverwrittenFiles { get; set; } = new List<string>();

        public string GetDetailedReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine($"Season Mod {(IsUpdate ? "Update" : "Installation")} Report - Year {SeasonYear}");
            report.AppendLine($"Status: {(Success ? "SUCCESS" : "FAILED")}");
            report.AppendLine($"Message: {Message}");
            report.AppendLine();

            if (IsUpdate)
            {
                report.AppendLine("⚠ This was an update to an existing season.");
                report.AppendLine();
            }

            if (CopiedFolders.Any())
            {
                report.AppendLine($"Copied Folders: {string.Join(", ", CopiedFolders)}");
            }

            if (OverwrittenFiles.Any())
            {
                report.AppendLine($"⚠ Overwritten Files ({OverwrittenFiles.Count}): {string.Join(", ", OverwrittenFiles.Take(5))}");
                if (OverwrittenFiles.Count > 5)
                {
                    report.AppendLine($"   ...and {OverwrittenFiles.Count - 5} more files");
                }
            }

            if (!string.IsNullOrEmpty(CleanupWarning))
            {
                report.AppendLine();
                report.AppendLine(CleanupWarning);
            }

            return report.ToString();
        }
    }

    public class SeasonExistsCheckResult
    {
        public bool Success { get; set; }
        public int SeasonYear { get; set; }
        public bool SeasonExists { get; set; }
        public Exception Exception { get; set; }
    }
}
