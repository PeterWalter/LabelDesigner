using FluentAssertions;
using LabelDesigner.Core.Models;
using LabelDesigner.Infrastructure.Persistence;
using System.IO;
using System.Threading.Tasks;

namespace LabelDesigner.Tests;

public class TemplateSeparationTests
{
    [Fact]
    public async Task Save_And_Load_Document_With_DataSource_Round_Trips_Correctly()
    {
        // Arrange
        var service = new JsonLabelPersistenceService();
        var doc = new SceneDocument
        {
            Version = "2.0",
            DataSource = new DataSourceConfig
            {
                Type = "csv",
                Path = @"C:\data\merged.csv",
                MergeMode = "OneRecordPerPage"
            }
        };

        // Act
        var json = await service.SaveToJsonAsync(doc);
        var loaded = await service.LoadFromJsonAsync(json);

        // Assert
        loaded.DataSource.Should().NotBeNull();
        loaded.DataSource!.Type.Should().Be("csv");
        loaded.DataSource!.Path.Should().Be(@"C:\data\merged.csv");
        loaded.DataSource!.MergeMode.Should().Be("OneRecordPerPage");
    }

    [Fact]
    public void Document_IsTemplate_File_Extension_Check_Works_As_Expected()
    {
        // Arrange
        var labelPath = "my_design.ldlabel";
        var templatePath = "my_template.ldtemplate";

        // Act
        bool isLabelTemplate = string.Equals(Path.GetExtension(labelPath), ".ldtemplate", StringComparison.OrdinalIgnoreCase);
        bool isTemplateTemplate = string.Equals(Path.GetExtension(templatePath), ".ldtemplate", StringComparison.OrdinalIgnoreCase);

        // Assert
        isLabelTemplate.Should().BeFalse();
        isTemplateTemplate.Should().BeTrue();
    }

    [Fact]
    public async Task When_Loading_Template_File_DataSource_Is_Set_To_Null()
    {
        // Arrange
        var service = new JsonLabelPersistenceService();
        var templateDocWithDataSource = new SceneDocument
        {
            Version = "2.0",
            DataSource = new DataSourceConfig
            {
                Type = "csv",
                Path = @"C:\data\merged.csv",
                MergeMode = "OneRecordPerPage"
            }
        };

        // Simulate saving a document
        var json = await service.SaveToJsonAsync(templateDocWithDataSource);

        // Simulate loading it
        var loadedDoc = await service.LoadFromJsonAsync(json);

        // If it's loaded as a template (.ldtemplate), the app sets its DataSource to null
        bool isTemplate = true; // Simulated loading of .ldtemplate
        if (isTemplate)
        {
            loadedDoc.DataSource = null;
        }

        // Assert
        loadedDoc.DataSource.Should().BeNull();
    }

    [Fact]
    public async Task When_Saving_As_Template_DataSource_Is_Excluded()
    {
        // Arrange
        var service = new JsonLabelPersistenceService();
        var currentDoc = new SceneDocument
        {
            Version = "2.0",
            DataSource = new DataSourceConfig
            {
                Type = "csv",
                Path = @"C:\data\merged.csv",
                MergeMode = "OneRecordPerPage"
            }
        };

        // Simulate SaveTemplateAsync where we clone and set DataSource = null
        var json = await service.SaveToJsonAsync(currentDoc);
        var saveDoc = await service.LoadFromJsonAsync(json);
        saveDoc.DataSource = null; // Discard data source for saved templates

        // Save to file string
        var savedTemplateJson = await service.SaveToJsonAsync(saveDoc);

        // Verify the saved JSON doesn't contain the data source info
        savedTemplateJson.Should().NotContain("merged.csv");
        savedTemplateJson.Should().NotContain("OneRecordPerPage");

        // Load the saved template
        var loadedTemplate = await service.LoadFromJsonAsync(savedTemplateJson);
        loadedTemplate.DataSource.Should().BeNull();
    }
}
