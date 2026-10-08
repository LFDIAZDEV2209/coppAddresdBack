using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Configurations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CoppAddresd.UnitTests.Media;

public class MediaChapterSerializationTests
{
    [Theory]
    [InlineData("[{\"atSeconds\":45,\"label\":\"Introducción\"}]")]
    [InlineData("[{\"AtSeconds\":45,\"Label\":\"Introducción\"}]")]
    public void Converter_ReadsHistoricalAndApplicationChapterNames(string json)
    {
        var converter = ChapterConverter();
        var chapters = Assert.IsType<List<MediaChapterDto>>(converter.ConvertFromProvider(json));
        var chapter = Assert.Single(chapters);
        Assert.Equal(45, chapter.AtSeconds);
        Assert.Equal("Introducción", chapter.Label);
        var saved = Assert.IsType<string>(converter.ConvertToProvider(chapters));
        var restored = Assert.IsType<List<MediaChapterDto>>(converter.ConvertFromProvider(saved));
        Assert.Equal(chapters, restored);
    }

    [Fact]
    public void Converter_ReadsEmptyChapterList()
    {
        Assert.Empty(Assert.IsType<List<MediaChapterDto>>(ChapterConverter().ConvertFromProvider("[]")));
    }

    private static Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter ChapterConverter()
    {
        var model = new ModelBuilder();
        new MediaItemConfiguration().Configure(model.Entity<MediaItem>());
        return model.Entity<MediaItem>().Property(item => item.Chapters).Metadata.GetValueConverter()!;
    }
}
