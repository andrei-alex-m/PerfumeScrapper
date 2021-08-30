using System;
using System.Collections;
using System.Collections.Generic;

namespace Scrapper
{
    public class ProductModel
    {
        public ProductModel()
        {
            Poze = new List<string>();
            Descrieri = new List<string>();
            //Breadcrumbs = new Dictionary<string, string>();
        }

        public IEnumerable<string> Poze;
        //public IDictionary<string, string> Breadcrumbs;
        public IEnumerable<string> Descrieri;
        public string Titlu;
        public string TitluScurt;
        public string Brand;
        public decimal Pret;
        public decimal PretInitial;
        public string Disponibilitate;
        public string Cod;
        public string Link;
        public string Model;
        public string Cantitate;
        public string Pentru;
        public string Olfactiv;
    }
}
