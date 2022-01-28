using System;
using TTG.SiteMap.Enums;

namespace TTG.SiteMap.Models
{
    public class SiteMap
    {
        public string Url { get; set; }
        public DateTime? Modified { get; set; }
        public ChangeFrequency? ChangeFrequency { get; set; }
        public double? Priority { get; set; }
    }
}
