using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;

namespace CodeGenerator
{
    public static class Generator
    {
        private static Settings? settings;

        public static Settings? LoadSettings()
        {
            var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            if (!File.Exists(settingsPath))
            {
                Console.WriteLine("appsettings.json not found.");
                return null;
            }

            var jsonString = File.ReadAllText(settingsPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            settings = JsonSerializer.Deserialize<Settings>(jsonString, options);
            return settings;
        }


        public static void GenerateFilesFromContext()
        {
            var contextPath = Path.Combine(Directory.GetCurrentDirectory(), "Infrastructure", "Context", "AppDbContext.cs");

            if (!File.Exists(contextPath))
            {
                Console.WriteLine($"AppDbContext.cs not found at path: {contextPath}");
                return;
            }

            var fileContent = File.ReadAllText(contextPath);
            string pattern = @"DbSet<\s*(\w+)\s*>\s+(\w+)\s*{";

            var matches = Regex.Matches(fileContent, pattern);

            foreach (Match match in matches)
            {
                string entityName = match.Groups[1].Value;
                string propertyName = match.Groups[2].Value;
                GenerateFiles(entityName, true);
                UpdateServiceRegistration(entityName);
                Console.WriteLine($"DbSet<{entityName}>: {propertyName}");
            }

        }
        public static void GenerateFiles(string entityName,bool isContext = false)
        {
            // Generate Entity
            if(!isContext) GenerateEntity(entityName);
            
            // Generate Service Interface
            GenerateIService(entityName);
            
            // Generate DTOs
            GenerateDto(entityName);

            // Generate Service Implementation
            GenerateService(entityName);


            Console.WriteLine($"Generated all files for entity: {entityName}");
        }

        private static void GenerateEntity(string entityName)
        {


            var template = $@"
using System;
using Domain.Entities;

namespace Domain.Entities
{{
    public class {entityName}:BaseEntity
    {{
        // Add your entity properties here
    }}
}}";

            SaveFile(settings?.TemplateSettings.Templates["Entity"], entityName, template);
        }

        private static void GenerateIService(string entityName)
        {
            var template = $@"
using Application.DTOs;                              
using Application.Wrapper;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Application.Interfaces
{{
    public interface I{entityName}Service
    {{
    }}
}}";
            var path = Path.Combine(settings?.TemplateSettings.Templates["IService"], $"I{entityName}Service.cs");
            if (!File.Exists(path))
            {
                SaveFile(settings?.TemplateSettings.Templates["IService"], $"I{entityName}Service", template);
            }
            
        }

        private static void GenerateService(string entityName)
        {
            var template = $@"
using Application.DTOs;
using Application.Interfaces;
using Application.Parameters;
using Application.Wrapper;
using AutoMapper;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Infrastructure.Services
{{
    public class {entityName}Service : GenericService<{entityName}, {entityName}Dto>, I{entityName}Service
    {{
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public {entityName}Service(IUnitOfWork unitOfWork, IMapper mapper)
            : base(unitOfWork, mapper)
        {{
        }}

        
    }}
}}";

            var path = Path.Combine(settings?.TemplateSettings.Templates["Service"], $"{entityName}Service.cs");
            if (!File.Exists(path))
            {
                SaveFile(settings?.TemplateSettings.Templates["Service"], $"{entityName}Service", template);
            }
            
        }

        private static void GenerateDto(string entityName)
        {
            var template = $@"
using Application.DTOs;
namespace Application.DTOs
{{
    public class {entityName}Dto:GeneralDto
    {{
        // Add your DTO properties here
    }}
}}";

            var path = Path.Combine(settings?.TemplateSettings.Templates["Dto"], $"{entityName}Dto.cs");
            if (!File.Exists(path))
            {
                SaveFile(settings?.TemplateSettings.Templates["Dto"], $"{entityName}Dto", template);
            }
            
        }

        public static void UpdateServiceRegistration(string entityName)
        {
            try
            {
                string filePath = Path.Combine(Directory.GetCurrentDirectory(), "Infrastructure", "DependancyInjection.cs");
                
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"DependancyInjection.cs not found at path: {filePath}");
                    return;
                }

                string interfaceName = $"I{entityName}Service";
                string implementationName = $"{entityName}Service";
                var code = File.ReadAllText(filePath);
                var tree = CSharpSyntaxTree.ParseText(code);
                var root = tree.GetRoot();

                var compilationUnit = (CompilationUnitSyntax)root;

                // Find the class
                var classNode = compilationUnit.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault(c => c.Identifier.Text == "DependancyInjection");

                if (classNode == null)
                {
                    Console.WriteLine("DependancyInjection class not found.");
                    return;
                }

                // Find the method
                var methodNode = classNode.Members
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(m => m.Identifier.Text == "AddApplicationServices");

                if (methodNode == null)
                {
                    Console.WriteLine("AddApplicationServices method not found.");
                    return;
                }

                // Check if service is already registered
                if (code.Contains($"AddScoped<{interfaceName}, {implementationName}>"))
                {
                    Console.WriteLine($"Service {entityName} is already registered.");
                    return;
                }

                // Build the new statement
                var registrationCode = $"services.AddScoped<{interfaceName}, {implementationName}>();";
                var registrationStatement = SyntaxFactory.ParseStatement(registrationCode + "\n");

                // Insert before return statement
                var newBody = methodNode.Body.WithStatements(
                    methodNode.Body.Statements.Insert(methodNode.Body.Statements.Count - 1, registrationStatement)
                );

                // Replace method
                var newMethod = methodNode.WithBody(newBody);
                var newClass = classNode.ReplaceNode(methodNode, newMethod);
                var newRoot = root.ReplaceNode(classNode, newClass);

                // Write back
                File.WriteAllText(filePath, newRoot.NormalizeWhitespace().ToFullString());
                Console.WriteLine($"Added service registration for {entityName}");

                // Update AppDbContext
                //UpdateAppDbContext(entityName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating service registration: {ex.Message}");
            }
        }

        public static void UpdateAppDbContext(string entityName)
        {
            try
            {
                var contextPath = Path.Combine(Directory.GetCurrentDirectory(), "Infrastructure", "Context", "AppDbContext.cs");
                
                if (!File.Exists(contextPath))
                {
                    Console.WriteLine($"AppDbContext.cs not found at path: {contextPath}");
                    return;
                }

                var code = File.ReadAllText(contextPath);
                var tree = CSharpSyntaxTree.ParseText(code);
                var root = tree.GetRoot();

                var compilationUnit = (CompilationUnitSyntax)root;

                // Find the AppDbContext class
                var classNode = compilationUnit.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault(c => c.Identifier.Text == "AppDbContext");

                if (classNode == null)
                {
                    Console.WriteLine("AppDbContext class not found.");
                    return;
                }

                // Check if DbSet already exists
                var propertyName = $"{entityName}s";
                var existingProperty = classNode.Members
                    .OfType<PropertyDeclarationSyntax>()
                    .FirstOrDefault(p => p.Identifier.Text == propertyName);

                if (existingProperty != null)
                {
                    Console.WriteLine($"DbSet for {entityName} already exists.");
                    return;
                }

                // Create new DbSet property
                var propertyDeclaration = SyntaxFactory.ParseMemberDeclaration(
                    $"public DbSet<{entityName}> {propertyName} {{ get; set; }}"
                );

                // Add the property to the class
                var newClass = classNode.AddMembers(propertyDeclaration);
                var newRoot = root.ReplaceNode(classNode, newClass);

                // Write back
                File.WriteAllText(contextPath, newRoot.NormalizeWhitespace().ToFullString());
                Console.WriteLine($"Added DbSet<{entityName}> to AppDbContext");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating AppDbContext: {ex.Message}");
            }
        }

        private static void SaveFile(string? path, string fileName, string content)
        {
            if (path == null || settings?.TemplateSettings.OutputPath == null) return;

            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), path);
            Directory.CreateDirectory(fullPath);

            var filePath = Path.Combine(fullPath, $"{fileName}.cs");
            File.WriteAllText(filePath, content);
            Console.WriteLine($"Generated: {filePath}");
        }
    }
}
