using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using System.Xml;
using System.Xml.Linq;
using TTG.SiteMap.Builders;
using TTG.SiteMap.Models;

using Microsoft.Extensions.Logging;

namespace TTG.SiteMap
{
    public class SiteMapGenerator : ISiteMapGenerator
    {
        private readonly ISelectQuery _query;
        private readonly ILogger _logger;
        public SiteMapGenerator(ISelectQuery query, ILogger<SiteMapGenerator> logger)
        {
            _query = query;
            _logger = logger;
        }   
                
        public virtual void Generate(string output, Configuration config, CancellationToken cancellationToken = default)
        {
            var rootSiteIndex = new SiteIndexBuilder();
            foreach (var batchConfiguration in config.Batches)
            {
                // run first batch
                _query.SetBatchConfiguration(batchConfiguration);
                _query.SetConnectionString(config.ConnectionString);
                int page = 0;
                var firstBatch = _query.RunQuery(page);
                if (firstBatch.Rows.Count == 0) continue;
                // if first batch less than  MaxBatch 
                if (firstBatch.Rows.Count < batchConfiguration.MaxNumberOfLinks)
                {
                    // then generate urlset map
                    var entityUrl = CreateUrlRuleSetFile(firstBatch, config.BaseUrl,
                        batchConfiguration.Name, batchConfiguration, output);
                    rootSiteIndex.AddIndex(new SiteIndex()
                    {
                        Modified = DateTime.Now,
                        Url = entityUrl,
                    });
                }
                else
                {
                    var childIndexBuilder = new SiteIndexBuilder();
                    var batchOutput = output;
                    // check if directory is available
                    if (!Directory.Exists(Path.Combine(output, batchConfiguration.Name)))
                    {
                        // if directory is not available then create new directory
                        batchOutput = Path.Combine(output, batchConfiguration.Name);
                        Directory.CreateDirectory(batchOutput);
                    }

                    // loop on batches
                    var nextBatch = firstBatch;
                    do
                    {
                        // generate first patch for ruleset
                        var entityBatchUrl = CreateUrlRuleSetFile(nextBatch, config.BaseUrl,
                            $"{batchConfiguration.Name}-{page + 1}", batchConfiguration, batchOutput, batchConfiguration.Name);
                        // add ti site index
                        childIndexBuilder.AddIndex(new SiteIndex()
                        {
                            Modified = DateTime.Now,
                            Url = entityBatchUrl,
                        });
                        nextBatch = _query.RunQuery(++page);
                    } while (nextBatch.Rows.Count > 0);

                    // generate sitemap for child index
                    var indexUrl = GenerateXMlFile(childIndexBuilder.Build(), config.BaseUrl, batchOutput,
                        batchConfiguration.Name, batchConfiguration.Compress, batchConfiguration.Name);
                    rootSiteIndex.AddIndex(new SiteIndex()
                    {
                        Modified = DateTime.Now,
                        Url = indexUrl,
                    });
                }
            }

            var sitemapUrl = GenerateXMlFile(rootSiteIndex.Build(), config.BaseUrl, output, "sitemap", false);
            Console.WriteLine($" Site map generated successfully at {sitemapUrl}");
        }

        protected virtual bool Compress(string inputFile, string compressedFile)
        {
            try
            {
                Console.WriteLine();
                using var input = File.OpenRead(inputFile);
                using var output = File.Create(compressedFile);
                using var compressor = new GZipStream(output, CompressionMode.Compress);
                input.CopyTo(compressor);
                return true;
            }
            catch (Exception e)
            {
                _logger.Log(LogLevel.Error, e, $"Failed to compress file {inputFile}");
                return false;
            }
        }

        protected  virtual string GenerateXMlFile(XDocument xml,
            string baseUrl, string output, string batchName, bool compress = true, string parentFolder = null)
        {
            using (var stream = new XmlTextWriter(File.Create(Path.Combine(output, $"{batchName}.xml")), Encoding.UTF8))
            {
                xml.Save(stream);
                stream.Flush();
                stream.Close();    
            }
            
            bool compressed = false;
            // compress generated file
            if (compress)
            {
                compressed = Compress(Path.Combine(output, $"{batchName}.xml"),
                    Path.Combine(output, $"{batchName}.xml.gz"));
            }

            if (compressed)
            {
                File.Delete(Path.Combine(output, $"{batchName}.xml"));
            }

            // Add the generated url to rootSiteIndex
            return $"{baseUrl}{(string.IsNullOrEmpty(parentFolder)? "": "/"+parentFolder)}/{batchName}.xml{(compressed ? ".gz" : "")}";
        }

        public virtual string GetUrl(string baseUrl, string url, DataRow row)
        {
            var dicValues = new Dictionary<string, string>();
            var regex = new Regex(@"\{([\w:()\-\""]+)\}");
            var matches = regex.Matches(url);
            foreach (Match match in matches)
            {
                var key = match.Groups[1].Value;
                // TODO handle format or function
                var value = row[key].ToString();
                dicValues.Add(key, value);
            }

            return
                $"{baseUrl}/{dicValues.Keys.Aggregate(url, (current, key) => current.Replace("{" + key + "}", dicValues[key]))}";
        }

        public virtual string CreateUrlRuleSetFile(DataTable entites, string baseUrl, string batchName,
            BatchConfiguration batchConfiguration, string output, string parentFolder = null)
        {
            var urlSet = new SiteMapBuilder();
            var modifiedDate = DateTime.Now;
            foreach (DataRow row in entites.Rows)
            {
                urlSet.AddUrl(
                    GetUrl(baseUrl, batchConfiguration.Url, row),
                    DateTime.TryParse(row[batchConfiguration.ModifiedDateColumn].ToString(), out modifiedDate)
                        ? modifiedDate
                        : DateTime.Now,
                    batchConfiguration.ChangeFrequency,
                    batchConfiguration.Priority);
            }

            // Generate XML
            return GenerateXMlFile(urlSet.Build(), baseUrl, output, batchName, batchConfiguration.Compress, parentFolder);
        }
    }
}