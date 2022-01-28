using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using TTG.SiteMap.Models;

namespace TTG.SiteMap.Builders
{
    public class SiteIndexBuilder
    {
        private readonly XNamespace NS = "http://www.sitemaps.org/schemas/sitemap/0.9";

        private List<SiteIndex> _index;

        public SiteIndexBuilder()
        {
            _index = new List<SiteIndex>();
        }
        public void AddIndex(SiteIndex index)
        {
            _index.Add(index);
        }

        public XDocument Build()
        {
            return new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XElement(NS + "sitemapindex",
                    from item in _index
                    select CreateItemElement(item)
                ));
        }
        
        public override string ToString()
        {
            return Build().ToString();
        }

        private XElement CreateItemElement(SiteIndex url)
        {
            XElement itemElement = new XElement(NS + "sitemap", new XElement(NS + "loc", url.Url.ToLower()));

            if (url.Modified.HasValue)
            {
                itemElement.Add(new XElement(NS + "lastmod", url.Modified.Value.ToString("yyyy-MM-ddTHH:mm:ss.f") + "+00:00"));
            }
            
            return itemElement;
        }
    }
}