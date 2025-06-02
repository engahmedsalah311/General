using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;

namespace CodeGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Please provide an entity name.");
                return;
            }

            var settings = Generator.LoadSettings();
            if (settings == null)
            {
                Console.WriteLine("Failed to load settings.");
                return;
            }

            var entityName = args[0];
            Generator.GenerateFiles(entityName);
            Generator.UpdateServiceRegistration(entityName);
            Generator.UpdateAppDbContext(entityName);
        }
    }

    public class Settings
    {
        public TemplateSettings TemplateSettings { get; set; } = new();
    }

    public class TemplateSettings
    {
        public string OutputPath { get; set; } = string.Empty;
        public string NamespacePrefix { get; set; } = string.Empty;
        public Dictionary<string, string> Templates { get; set; } = new();
    }
}
