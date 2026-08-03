using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Automatyczne_Klawisze
{
    public class EnovaConfigReader
    {
        // Metoda teraz przyjmuje ścieżkę do pliku jako argument
        public static List<string> PobierzBazyZXml(string sciezkaXml)
        {
            List<string> listaBaz = new List<string>();

            if (File.Exists(sciezkaXml))
            {
                XDocument doc = XDocument.Load(sciezkaXml);

                listaBaz = doc.Descendants("Name")
                              .Select(x => x.Value.Trim())
                              .Where(nazwa => !string.IsNullOrEmpty(nazwa))
                              .Distinct()
                              .ToList();
            }

            return listaBaz;
        }
    }
}