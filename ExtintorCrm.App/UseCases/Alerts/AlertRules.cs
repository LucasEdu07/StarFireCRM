using System;
using System.Linq;

namespace ExtintorCrm.App.UseCases.Alerts
{
    public class AlertRules
    {
        public int[] AlertDays { get; private set; } = { 7, 15, 30 };

        public int MaxAlertDays => AlertDays.Any() ? AlertDays.Max() : -1;

        public void SetAlertDays(params int[] days)
        {
            AlertDays = days
                .Where(x => x > 0)
                .Distinct()
                .OrderBy(x => x)
                .ToArray();
        }
    }
}
