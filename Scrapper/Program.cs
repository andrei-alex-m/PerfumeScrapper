using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Newtonsoft.Json;

namespace Scrapper
{
    class Program
    {
        const string site = "https://www.noteorientale.ro/parfumuri-si-seturi-cadou/";
        const string root = "https://www.noteorientale.ro";
        static void Main(string[] args)
        {

            Task.Run(() => IteratePages()).GetAwaiter().GetResult();
        }

        private static async Task<string> CallUrl(string fullUrl)
        {
            HttpClient client = new HttpClient();
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls13;
            client.DefaultRequestHeaders.Accept.Clear();
            var response = client.GetStringAsync(fullUrl);
            return await response;
        }

        private static IEnumerable<string> ParseProductLinks(HtmlDocument htmlDoc)
        {
            return htmlDoc.DocumentNode.Descendants("div")
                    .Where(node => node.GetAttributeValue("class", "").Contains("product_name"))
                    .SelectMany(x => x.Descendants("a"))
                    .SelectMany(x => x.Attributes.Where(y => y.Name == "href"))
                    .Select(x => x.Value);
        }

        private static string ParseNextPageLink(HtmlDocument htmlDoc)
        {
            string nextLink = htmlDoc.DocumentNode.SelectSingleNode("/html/head").Descendants("link")
            .Where(x => x.Attributes.Any(x => x.Name == "rel" && x.Value == "next"))
            .FirstOrDefault()?.GetAttributeValue("href", "");

            return nextLink;

        }

        private static async Task IteratePages()
        {
            var nextlink = site;
            var results = new ConcurrentBag<ProductModel>();

            while (!String.IsNullOrEmpty(nextlink))
            {
                var response = await CallUrl(nextlink);
                HtmlDocument htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(response);
                var productList = ParseProductLinks(htmlDoc);
                nextlink = ParseNextPageLink(htmlDoc);


                Parallel.ForEach(productList, new ParallelOptions() { MaxDegreeOfParallelism = 8 }, async x =>
                {
                    var produs = await ScrapProductPage(x);
                    results.Add( produs);
                    //File.AppendAllText($"produse.json", JsonConvert.SerializeObject(produs, Formatting.Indented));

                });

                Console.WriteLine(nextlink);
                Console.WriteLine(results.Count);

            }
            File.WriteAllText($"produse.json", JsonConvert.SerializeObject(results, Formatting.Indented));

        }

        private static async Task<ProductModel> ScrapProductPage(string url)
        {
            var result = new ProductModel();
            try
            {
                var response = await CallUrl(url);
                HtmlDocument htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(response);
                FillProductDetails(result, htmlDoc);
                //FillBreadcrumbs(result, htmlDoc);
                FillProductChars(result, htmlDoc);
                FillProductImages(result, htmlDoc);
                FillProductDescriptions(result, htmlDoc);
            }
            catch (Exception ex)
            {

            }
            result.Link = url;
            return result;
        }

        private static void FillProductDetails(ProductModel produs, HtmlDocument htmlDoc)
        {
            var detailsNode = htmlDoc.DocumentNode.Descendants("div")
                .Where(node => node.GetAttributeValue("class", "") == "prod_main_details").FirstOrDefault();
            produs.Titlu = detailsNode.Descendants("h1")
                .FirstOrDefault(x => x.Attributes.Any(x => x.Name == "itemprop" && x.Value == "name"))?
                .InnerText;

            var pretInitialText = detailsNode.Descendants("h3")
                .FirstOrDefault(node => node.GetAttributeValue("class", "") == "price intial")?
                .Descendants().FirstOrDefault()?.GetDirectInnerText();

            if (!string.IsNullOrEmpty(pretInitialText))
            {
                decimal.TryParse(pretInitialText.Trim(), out produs.PretInitial);
            }

            var pretText = detailsNode.Descendants("h3")
                .FirstOrDefault(node => node.GetAttributeValue("class", "") == "price")?
                .Descendants().FirstOrDefault()?.GetDirectInnerText();

            if (!string.IsNullOrEmpty(pretText))
            {
                decimal.TryParse(pretText.Trim(), out produs.Pret);
            }

            produs.Disponibilitate = detailsNode.Descendants("span")
                .FirstOrDefault(node => node.GetAttributeValue("class", "").StartsWith("af_"))?
                .GetDirectInnerText();

            produs.Brand = detailsNode.Descendants("span")
                .FirstOrDefault(node => node.GetAttributeValue("itemprop", "") == "brand")?
                .GetDirectInnerText();

            produs.TitluScurt = detailsNode.Descendants("span")
                .FirstOrDefault(node => node.GetAttributeValue("itemprop", "") == "model")?
                .GetDirectInnerText();

            produs.Cod = detailsNode.Descendants("span")
                .FirstOrDefault(node => node.InnerText == "Cod produs:")?
                .ParentNode.LastChild.GetDirectInnerText();

        }

        //private static void FillBreadcrumbs(ProductModel produs, HtmlDocument htmlDoc)
        //{
        //    produs.Breadcrumbs = htmlDoc.DocumentNode.Descendants("div")
        //        .FirstOrDefault(node => node.Id == "breadcrumbs")?
        //        .Descendants("a").ToDictionary(x => x.GetAttributeValue("href", ""), x => x.GetDirectInnerText());
        //}

        private static void FillProductChars(ProductModel produs, HtmlDocument htmlDoc)
        {
            var charsNode = htmlDoc.DocumentNode.Descendants("table")
                .FirstOrDefault(node => node.GetAttributeValue("class", "") == "chars")?
                .Descendants("tr").FirstOrDefault();

            if (charsNode == null)
                return;

            var charsT = charsNode.Elements("div").SelectMany(x => x.Elements("div")).Select(y =>
               new Tuple<string, string>
               (
                   Cleanup(y.Descendants("span").FirstOrDefault()?.GetDirectInnerText().ToLowerInvariant()),
                   Cleanup(y.Descendants("div").LastOrDefault()?.GetDirectInnerText())
               ));

            var chars = charsT.ToDictionary(z=>z.Item1, z=>z.Item2);

            chars.TryGetValue("model parfum", out produs.Model);
            chars.TryGetValue("cantitate", out produs.Cantitate);
            chars.TryGetValue("pentru", out produs.Pentru);
            chars.TryGetValue("categorie olfactiva", out produs.Olfactiv);
        }

        private static void FillProductImages(ProductModel produs, HtmlDocument htmlDoc)
        {
            var imgNode = htmlDoc.DocumentNode.Descendants("div")
                .FirstOrDefault(node => node.GetAttributeValue("class", "") == "image-carousel") ??
            htmlDoc.DocumentNode.Descendants("td")
                .FirstOrDefault(node => node.GetAttributeValue("class", "") == "pm_img");

            produs.Poze = imgNode.Descendants("img")
                .Select(x => $"{root}{x.GetAttributeValue("data-src", "")}").ToList();
        }

        private static void FillProductDescriptions(ProductModel produs, HtmlDocument htmlDoc)
        {
            var descNode = htmlDoc.DocumentNode.Descendants("div")
                    .Where(node => node.GetAttributeValue("class", "") == "prod_main_desc").FirstOrDefault();

            if (descNode==null)
                return;

            produs.Descrieri = descNode.Descendants("p").Select(x => Cleanup(x.GetDirectInnerText()))
                .Where(x => !string.IsNullOrEmpty(x));
        }

        static string Cleanup(string input)
        {
            var sb = new StringBuilder(input);

            sb.Replace("\n", "");
            sb.Replace("\r", "");
            sb.Replace("&nbsp", "");

            var result = sb.ToString();
            result = result.Trim(' ', ';');
            return result;
        }
    }
}
