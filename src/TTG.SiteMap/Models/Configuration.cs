using System.Collections.Generic;
using TTG.SiteMap.Enums;

namespace TTG.SiteMap.Models
{
    public class Configuration
    {
        public string BaseUrl { get; set; }
        public string ConnectionString { get; set; }
        public ICollection<BatchConfiguration> Batches { get; set; }

    }

    public class BatchConfiguration
    {
        public string Name { get; set; }
        public int MaxNumberOfLinks { get; set; } = 50000;
        public string Url { get; set; }
        public string SelectQuery { get; set; }
        public string OrderByColumn { get; set; }
        public string ModifiedDateColumn { get; set; }
        public ChangeFrequency ChangeFrequency { get; set; } = ChangeFrequency.Weekly;
        public int Priority { get; set; } = 1;
        public bool Compress { get; set; } = true;
    }
}