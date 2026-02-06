using Labyrinth.Api.Models;
using Labyrinth.Api.Services;
using NUnit.Framework;

namespace Labyrinth.Api.Tests;

[TestFixture]
public class LocalLabyrinthServiceTests
{
    private LocalLabyrinthService _service = null!;
    private Guid _testAppKey;
    
    [SetUp]
    public void Setup()
    {
        _service = new LocalLabyrinthService();
        _testAppKey = Guid.NewGuid();
    }
    
    [Test]
    public async Task CreateCrawler_ShouldReturnNewCrawler()
    {
        var crawler = await _service.CreateCrawlerAsync(_testAppKey, null);
        
        Assert.That(crawler, Is.Not.Null);
        Assert.That(crawler.Id, Is.Not.EqualTo(Guid.Empty));
        Assert.That(crawler.AppKey, Is.EqualTo(_testAppKey));
    }
    
    [Test]
    public async Task CreateCrawler_ShouldThrowException_WhenMaxCrawlersReached()
    {
        await _service.CreateCrawlerAsync(_testAppKey, null);
        await _service.CreateCrawlerAsync(_testAppKey, null);
        await _service.CreateCrawlerAsync(_testAppKey, null);
        
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.CreateCrawlerAsync(_testAppKey, null));
    }
    
    [Test]
    public async Task GetCrawlers_ShouldReturnAllCrawlersForAppKey()
    {
        await _service.CreateCrawlerAsync(_testAppKey, null);
        await _service.CreateCrawlerAsync(_testAppKey, null);
        
        var crawlers = await _service.GetCrawlersAsync(_testAppKey);
        
        Assert.That(crawlers.Count(), Is.EqualTo(2));
    }
    
    [Test]
    public async Task GetCrawler_ShouldReturnCrawler_WhenExists()
    {
        var created = await _service.CreateCrawlerAsync(_testAppKey, null);
        
        var retrieved = await _service.GetCrawlerAsync(created.Id, _testAppKey);
        
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.Id, Is.EqualTo(created.Id));
    }
    
    [Test]
    public async Task GetCrawler_ShouldReturnNull_WhenWrongAppKey()
    {
        var created = await _service.CreateCrawlerAsync(_testAppKey, null);
        var wrongAppKey = Guid.NewGuid();
        
        
        var retrieved = await _service.GetCrawlerAsync(created.Id, wrongAppKey);
        
        Assert.That(retrieved, Is.Null);
    }
    
    [Test]
    public async Task UpdateCrawler_ShouldChangeDirection()
    {
        var crawler = await _service.CreateCrawlerAsync(_testAppKey, null);
        var update = new CrawlerUpdateDto(Direction: 1, IsWalking: null); // East
        
        var updated = await _service.UpdateCrawlerAsync(crawler.Id, _testAppKey, update);
        
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.Direction, Is.EqualTo(1));
    }
    
    [Test]
    public async Task DeleteCrawler_ShouldReturnTrue_WhenSuccessful()
    {
        
        var crawler = await _service.CreateCrawlerAsync(_testAppKey, null);
        
        var deleted = await _service.DeleteCrawlerAsync(crawler.Id, _testAppKey);
        
        Assert.That(deleted, Is.True);
        
        var retrieved = await _service.GetCrawlerAsync(crawler.Id, _testAppKey);
        Assert.That(retrieved, Is.Null);
    }
    
    [Test]
    public async Task GetCrawlerBag_ShouldReturnEmptyInitially()
    {
        var crawler = await _service.CreateCrawlerAsync(_testAppKey, null);
        
        var bag = await _service.GetCrawlerBagAsync(crawler.Id, _testAppKey);
        
        Assert.That(bag, Is.Not.Null);
        Assert.That(bag!.Count(), Is.EqualTo(0));
    }
    
    [Test]
    public async Task UpdateCrawlerBag_ShouldAddItems()
    {
        var crawler = await _service.CreateCrawlerAsync(_testAppKey, null);
        var items = new[] { new InventoryItemDto("Key"), new InventoryItemDto("Key") };
        
        var success = await _service.UpdateCrawlerBagAsync(crawler.Id, _testAppKey, items);
        
        Assert.That(success, Is.True);
        
        var bag = await _service.GetCrawlerBagAsync(crawler.Id, _testAppKey);
        Assert.That(bag!.Count(), Is.EqualTo(2));
    }
}
