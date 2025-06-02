# Code Generator for Clean Architecture

This is a code generator tool that automatically creates the necessary classes and interfaces when adding a new entity to your Clean Architecture solution.

## What it generates

For each entity, it generates:

1. Entity class in the Domain layer
2. Repository interface in the Domain layer
3. Repository implementation in the Infrastructure layer
4. Service interface in the Application layer
5. Service implementation in the Application layer
6. DTO (Data Transfer Object) in the Application layer
7. Controller in the Application layer

## How to use

1. Build the project:
```bash
dotnet build
```

2. Run the generator by providing an entity name:
```bash
dotnet run --project CodeGenerator YourEntityName
```

For example:
```bash
dotnet run --project CodeGenerator Product
```

This will generate all the necessary files for a `Product` entity.

## Configuration

The generator uses `appsettings.json` for configuration. You can modify the following settings:

- `OutputPath`: The root path where files will be generated
- `NamespacePrefix`: The prefix for all namespaces
- `Templates`: The directory paths for each type of file

## Generated Structure

```
├── Domain
│   ├── Entities
│   │   └── YourEntity.cs
│   └── Repositories
│       └── IYourEntityRepository.cs
├── Infrastructure
│   └── Repositories
│       └── YourEntityRepository.cs
└── Application
    ├── DTOs
    │   └── YourEntityDto.cs
    ├── Interfaces
    │   └── IYourEntityService.cs
    ├── Services
    │   └── YourEntityService.cs
    └── Controllers
        └── YourEntityController.cs
```

## Customization

You can customize the generated code by modifying the templates in the `Program.cs` file. Each type of file has its own generation method that you can modify to suit your needs. 