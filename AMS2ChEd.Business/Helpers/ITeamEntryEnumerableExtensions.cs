using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AMS2ChEd.Business.Helpers
{
    public static class ITeamEntryEnumerableExtensions
    {
        public static int DriverCount(this IEnumerable<ITeamEntry> teams)
        {
            return teams.Sum(t =>
                (string.IsNullOrEmpty(t.Driver1Contract?.DriverId) ? 0 : 1) +
                (string.IsNullOrEmpty(t.Driver2Contract?.DriverId) ? 0 : 1));
        }

        public static int DriverCount(this IEnumerable<EntryListEntry> teams)
        {
            return teams.Sum(e =>
                (string.IsNullOrEmpty(e?.Driver1Id) ? 0 : 1) +
                (string.IsNullOrEmpty(e?.Driver2Id) ? 0 : 1));
        }
    }
}
