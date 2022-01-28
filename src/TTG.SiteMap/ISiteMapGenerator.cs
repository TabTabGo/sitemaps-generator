using System.Threading;
using TTG.SiteMap.Models;

namespace TTG.SiteMap
{
    public interface ISiteMapGenerator
    {
        void Generate(string output, Configuration config, CancellationToken cancellationToken = default);
    }
}