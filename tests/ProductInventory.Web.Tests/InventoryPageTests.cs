using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ProductInventory.Contracts;
using ProductInventory.Web.Pages;
using ProductInventory.Web.Services;
using ProductInventory.Web.Tests.Fakes;
using Xunit;

namespace ProductInventory.Web.Tests;

public sealed class InventoryPageTests : BunitContext
{
    [Fact]
    public void Shows_loading_state_until_the_initial_request_finishes()
    {
        var repository = new FakeProductRepository(SampleProducts()) { HoldInitialLoad = true };
        Services.AddSingleton<IProductRepository>(repository);

        var page = Render<Home>();

        Assert.Contains("Loading inventory", FindByRole(page, "status").TextContent);
        Assert.Contains(
            "up to one minute",
            page.Find("aside[aria-label='Demo API availability']").TextContent);

        repository.CompleteInitialLoad();

        page.WaitForAssertion(() =>
        {
            Assert.Contains("Mechanical keyboard", page.Markup);
            Assert.Contains("2", FindSummaryCard(page, "Total products").TextContent);
            Assert.Contains("1", FindSummaryCard(page, "Active products").TextContent);
            Assert.Contains("$570.00", FindSummaryCard(page, "Inventory value").TextContent);
        });
    }

    [Fact]
    public void Filters_by_name_on_input_case_insensitively_without_another_request()
    {
        var repository = new FakeProductRepository(SampleProducts());
        Services.AddSingleton<IProductRepository>(repository);
        var page = Render<Home>();
        page.WaitForAssertion(() => Assert.Contains("Mechanical keyboard", page.Markup));
        var listCallsAfterLoad = repository.ListCalls;

        FindControlByLabel(page, "Search products").Input("KEYBOARD");

        Assert.Contains("Mechanical keyboard", page.Markup);
        Assert.DoesNotContain("Monitor stand", page.Markup);
        Assert.Contains("1 product found", page.Markup);
        Assert.Equal(listCallsAfterLoad, repository.ListCalls);
    }

    [Fact]
    public void Invalid_form_shows_field_errors_and_does_not_create_a_product()
    {
        var repository = new FakeProductRepository(SampleProducts());
        Services.AddSingleton<IProductRepository>(repository);
        var page = Render<Home>();
        page.WaitForAssertion(() => Assert.Contains("Mechanical keyboard", page.Markup));

        FindButton(page, "Add product").Click();
        page.Find("form").Submit();

        page.WaitForAssertion(() =>
        {
            Assert.Contains("Name is required.", page.Markup);
            Assert.Contains("Price must be greater than zero.", page.Markup);
            Assert.Equal(0, repository.CreateCalls);
        });
    }

    [Fact]
    public void Create_edit_and_delete_reload_the_canonical_inventory_and_update_the_dashboard()
    {
        var repository = new FakeProductRepository(
        [
            new ProductResponse(Guid.NewGuid(), "Desk lamp", 25m, 2, true)
        ]);
        Services.AddSingleton<IProductRepository>(repository);
        var page = Render<Home>();
        page.WaitForAssertion(() => Assert.Contains("Desk lamp", page.Markup));

        FindButton(page, "Add product").Click();
        FindControlByLabel(page, "Name").Change("Monitor arm");
        FindControlByLabel(page, "Price (USD)").Change("75.50");
        FindControlByLabel(page, "Quantity").Change("3");
        page.Find("form").Submit();

        page.WaitForAssertion(() =>
        {
            Assert.Contains("Monitor arm was created.", page.Markup);
            Assert.Contains("2", FindSummaryCard(page, "Total products").TextContent);
            Assert.Equal(1, repository.CreateCalls);
            Assert.True(repository.ListCalls >= 2);
        });

        var createdRow = FindProductRow(page, "Monitor arm");
        FindButton(createdRow, "Edit").Click();
        FindControlByLabel(page, "Name").Change("Premium monitor arm");
        page.Find("form").Submit();

        page.WaitForAssertion(() =>
        {
            Assert.Contains("Premium monitor arm was updated.", page.Markup);
            Assert.Equal(1, repository.UpdateCalls);
        });

        var updatedRow = FindProductRow(page, "Premium monitor arm");
        FindButton(updatedRow, "Delete").Click();

        updatedRow = FindProductRow(page, "Premium monitor arm");
        Assert.Contains("Delete “Premium monitor arm”?", updatedRow.TextContent);
        Assert.Equal("alert", updatedRow.QuerySelector("[role='alert']")?.GetAttribute("role"));
        FindButton(updatedRow, "Delete").Click();

        page.WaitForAssertion(() =>
        {
            Assert.DoesNotContain(
                page.FindAll("tbody tr"),
                row => row.TextContent.Contains("Premium monitor arm", StringComparison.Ordinal));
            Assert.Contains("Premium monitor arm was deleted.", page.Markup);
            Assert.Contains("1", FindSummaryCard(page, "Total products").TextContent);
            Assert.Equal(1, repository.DeleteCalls);
            Assert.True(repository.ListCalls >= 4);
        });
    }

    [Fact]
    public void Retry_recovers_after_the_initial_request_fails()
    {
        var repository = new FakeProductRepository(SampleProducts())
        {
            FailingListCallsRemaining = 1
        };
        Services.AddSingleton<IProductRepository>(repository);

        var page = Render<Home>();
        page.WaitForAssertion(() =>
            Assert.Contains("The demo API is getting ready", FindByRole(page, "status").TextContent));

        FindButton(page, "Try again").Click();

        page.WaitForAssertion(() =>
        {
            Assert.Contains("Mechanical keyboard", page.Markup);
            Assert.Equal(2, repository.ListCalls);
        });
    }

    [Fact]
    public void A_second_initial_connection_failure_remains_a_neutral_retry_state()
    {
        var repository = new FakeProductRepository(SampleProducts())
        {
            FailingListCallsRemaining = 2
        };
        Services.AddSingleton<IProductRepository>(repository);

        var page = Render<Home>();
        page.WaitForAssertion(() => Assert.Contains("The demo API is getting ready", page.Markup));

        FindButton(page, "Try again").Click();

        page.WaitForAssertion(() =>
        {
            Assert.Contains("The demo API is getting ready", FindByRole(page, "status").TextContent);
            Assert.Empty(page.FindAll("[role='alert']"));
        });
    }

    [Fact]
    public void Sorts_by_price_ascending_then_descending_on_header_click()
    {
        var repository = new FakeProductRepository(SampleProducts());
        Services.AddSingleton<IProductRepository>(repository);
        var page = Render<Home>();
        page.WaitForAssertion(() => Assert.Contains("Mechanical keyboard", page.Markup));

        PriceHeaderButton(page).Click();
        Assert.StartsWith("Monitor stand", FirstRowName(page), StringComparison.Ordinal);

        PriceHeaderButton(page).Click();
        Assert.StartsWith("Mechanical keyboard", FirstRowName(page), StringComparison.Ordinal);
    }

    private static IElement PriceHeaderButton(IRenderedComponent<Home> page) =>
        page.FindAll("th button")
            .First(button => button.TextContent.Trim().StartsWith("Price", StringComparison.Ordinal));

    private static string FirstRowName(IRenderedComponent<Home> page) =>
        page.FindAll("tbody tr").First().QuerySelector(".product-name")!.TextContent.Trim();

    private static ProductResponse[] SampleProducts() =>
    [
        new ProductResponse(Guid.NewGuid(), "Mechanical keyboard", 120m, 4, true),
        new ProductResponse(Guid.NewGuid(), "Monitor stand", 45m, 2, false)
    ];

    private static IElement FindByRole(IRenderedComponent<Home> page, string role) =>
        page.FindAll($"[role='{role}']").First();

    private static IElement FindSummaryCard(IRenderedComponent<Home> page, string label) =>
        page.FindAll("article").Single(card => card.TextContent.Contains(label, StringComparison.Ordinal));

    private static IElement FindControlByLabel(IRenderedComponent<Home> page, string labelText)
    {
        var label = page.FindAll("label")
            .Single(element => element.TextContent.Trim().StartsWith(labelText, StringComparison.Ordinal));
        var controlId = label.GetAttribute("for")
            ?? throw new InvalidOperationException($"The '{labelText}' label isn't associated with a control.");
        return page.Find($"#{controlId}");
    }

    private static IElement FindProductRow(IRenderedComponent<Home> page, string productName) =>
        page.FindAll("tbody tr")
            .Single(row => row.TextContent.Contains(productName, StringComparison.Ordinal));

    private static IElement FindButton(IRenderedComponent<Home> page, string accessibleText) =>
        page.FindAll("button")
            .First(button => string.Equals(button.TextContent.Trim(), accessibleText, StringComparison.Ordinal));

    private static IElement FindButton(IElement container, string accessibleText) =>
        container.QuerySelectorAll("button")
            .First(button => string.Equals(button.TextContent.Trim(), accessibleText, StringComparison.Ordinal));
}
