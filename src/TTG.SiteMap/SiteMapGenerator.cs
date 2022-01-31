using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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

        /// <summary>
        /// Parse url for site map url set and populate the site map keys 
        /// </summary>
        /// <param name="baseUrl"></param>
        /// <param name="url"></param>
        /// <param name="row"></param>
        /// <returns></returns>
        public virtual string ParseUrl(string baseUrl, string url, DataRow row)
        {
            var dicValues = new Dictionary<string, string>();
            var regex = new Regex(@"\{([\w:()\-\""']+)\}");
            var matches = regex.Matches(url);
            string key = null;
            string value = string.Empty;

            foreach (Match match in matches)
            {
                var expr = match.Groups[1].Value;
                if (expr.Contains(":"))
                {
                    var keyParts = expr.Split(':');
                    if (keyParts.Length == 2)
                    {
                         key = keyParts[0];
                         // regex to get function and params
                         var functionRegex = new Regex(@"^([\w]+)\((.*)\)$");
                         var functionMatch = functionRegex.Match(keyParts[1]);
                         if (functionMatch.Success)
                         {
                             var function = functionMatch.Groups[1].Value;
                             var functionParams = functionMatch.Groups[2].Value.Split(',');
                             value = GetFunctionValue(function, functionParams, row[key].ToString());
                         }
                         else
                         {
                             value = row[key].ToString();
                         }
                    }
                }
                else
                {
                    key = expr;
                    value = row[key].ToString();
                }
                dicValues.Add(expr, value);
                
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
                    ParseUrl(baseUrl, batchConfiguration.Url, row),
                    DateTime.TryParse(row[batchConfiguration.ModifiedDateColumn].ToString(), out modifiedDate)
                        ? modifiedDate
                        : DateTime.Now,
                    batchConfiguration.ChangeFrequency,
                    batchConfiguration.Priority);
            }

            // Generate XML
            return GenerateXMlFile(urlSet.Build(), baseUrl, output, batchName, batchConfiguration.Compress, parentFolder);
        }
        
        /// <summary>
        /// Run string extension method to get function value
        /// </summary>
        /// <param name="function">function name</param>
        /// <param name="functionParams">list of parameter value</param>
        /// <param name="dataValue">data value</param>
        /// <returns></returns>
        private string GetFunctionValue(string function, string[] functionParams, string dataValue)
        {
            if (string.IsNullOrEmpty(dataValue)) return string.Empty;
            var methods = GetExtensionMethods(typeof(SiteMapGenerator).Assembly, typeof(string));
            var method = methods.FirstOrDefault(m => m.Name == function);
            if (method != null)
            {
                var parameters = new List<object>();
                if (method.IsStatic && method.IsDefined(typeof(ExtensionAttribute), false))
                {
                    parameters.Add(dataValue);
                }

                foreach (var param in functionParams.Where(t => !string.IsNullOrEmpty(t)))
                {
                    //remove ' or " from param value
                    var paramValue = param.Trim('\'').Trim('"');
                    parameters.Add(paramValue);
                }
                
                return method.Invoke(dataValue,parameters.ToArray()).ToString();
            }
            return dataValue;
        }
        
        /// <summary>
        /// Get list of extensions in an assembly and function for a type
        /// </summary>
        /// <param name="assembly"></param>
        /// <param name="extendedType"></param>
        /// <returns></returns>
        private IEnumerable<MethodInfo> GetExtensionMethods(Assembly assembly,
            Type extendedType)
        {
            var query = assembly.GetTypes()
                .Where(type => type.IsSealed && !type.IsGenericType && !type.IsNested)
                .SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                .Where(method => method.IsDefined(typeof(ExtensionAttribute), false) &&
                                 method.GetParameters()[0].ParameterType == extendedType).ToList();
             
            query.AddRange(extendedType.GetMethods().Where(method => method.IsPublic));
            return query;
        }
    }
}