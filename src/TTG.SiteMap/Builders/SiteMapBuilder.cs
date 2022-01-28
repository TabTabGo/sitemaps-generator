using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using TTG.SiteMap.Enums;

namespace TTG.SiteMap.Builders
{
    public class SiteMapBuilder
    {
        private readonly XNamespace NS = "http://www.sitemaps.org/schemas/sitemap/0.9";

        private List<Models.SiteMap> _urls;

        public SiteMapBuilder()
        {
            _urls = new List<Models.SiteMap>();
        }

        public void AddUrl(Models.SiteMap url)
        {
            _urls.Add(url);
        }
        public void AddUrl(string url, DateTime? modified = null, ChangeFrequency? changeFrequency = null, double? priority = null)
        {
            _urls.Add(new Models.SiteMap()
            {
                Url = url,
                Modified = modified,
                ChangeFrequency = changeFrequency,
                Priority = priority,
            });
        }

        public XDocument Build()
        {
            return new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XElement(NS + "urlset",
                    _urls.Select(CreateItemElement)
                ));
        }
        public override string ToString()
        {
            return Build().ToString();
        }

        private XElement CreateItemElement(Models.SiteMap url)
        {
            XElement itemElement = new XElement(NS + "url", new XElement(NS + "loc", url.Url.ToLower()));

            if (url.Modified.HasValue)
            {
                itemElement.Add(new XElement(NS + "lastmod", url.Modified.Value.ToString("yyyy-MM-ddTHH:mm:ss.f") + "+00:00"));
            }

            if (url.ChangeFrequency.HasValue)
            {
                itemElement.Add(new XElement(NS + "changefreq", url.ChangeFrequency.Value.ToString().ToLower()));
            }

            if (url.Priority.HasValue)
            {
                itemElement.Add(new XElement(NS + "priority", url.Priority.Value.ToString("N1")));
            }

            return itemElement;
        }

        
    }
}