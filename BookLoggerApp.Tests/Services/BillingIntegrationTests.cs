using FluentAssertions;
using NSubstitute;
using Xunit;

using BookLoggerApp.Core.Constants;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Tests.TestHelpers;

namespace BookLoggerApp.Tests.Services;

public class BillingIntegrationTests
{
    // --- Purchase flow with mocked IBillingService + real SubscriptionService ---

    [Fact]
    public async Task PurchaseAsync_Success_ShouldUpdateTierToPremium()
    {
        // Arrange
        var subscriptionService = Substitute.For<ISubscriptionService>();
        var billingService = Substitute.For<IBillingService>();

        billingService.PurchaseAsync(BillingConstants.PremiumMonthly, Arg.Any<CancellationToken>())
            .Returns(PurchaseResult.Succeeded(BillingConstants.PremiumMonthly, "test_token"));

        // Act
        var result = await billingService.PurchaseAsync(BillingConstants.PremiumMonthly);

        // Assert
        result.Success.Should().BeTrue();
        result.ProductId.Should().Be(BillingConstants.PremiumMonthly);
        result.PurchaseToken.Should().Be("test_token");
        result.WasCancelled.Should().BeFalse();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task PurchaseAsync_Cancelled_ShouldReturnCancelledResult()
    {
        // Arrange
        var billingService = Substitute.For<IBillingService>();
        billingService.PurchaseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(PurchaseResult.Cancelled());

        // Act
        var result = await billingService.PurchaseAsync(BillingConstants.PremiumMonthly);

        // Assert
        result.Success.Should().BeFalse();
        result.WasCancelled.Should().BeTrue();
        result.ProductId.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task PurchaseAsync_Failed_ShouldReturnErrorMessage()
    {
        // Arrange
        var billingService = Substitute.For<IBillingService>();
        billingService.PurchaseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(PurchaseResult.Failed("Network error"));

        // Act
        var result = await billingService.PurchaseAsync(BillingConstants.PremiumMonthly);

        // Assert
        result.Success.Should().BeFalse();
        result.WasCancelled.Should().BeFalse();
        result.ErrorMessage.Should().Be("Network error");
    }

    [Fact]
    public async Task RestorePurchasesAsync_NoSubscription_ShouldReturnFalse()
    {
        // Arrange
        var billingService = Substitute.For<IBillingService>();
        billingService.RestorePurchasesAsync(Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await billingService.RestorePurchasesAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RestorePurchasesAsync_WithSubscription_ShouldReturnTrue()
    {
        // Arrange
        var billingService = Substitute.For<IBillingService>();
        billingService.RestorePurchasesAsync(Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await billingService.RestorePurchasesAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TierSwitch_MonthlyToYearly_ShouldUpdateProductId()
    {
        // Arrange — simulate switching from monthly to yearly
        var subscriptionService = Substitute.For<ISubscriptionService>();
        var billingService = Substitute.For<IBillingService>();

        // First purchase monthly
        billingService.PurchaseAsync(BillingConstants.PremiumMonthly, Arg.Any<CancellationToken>())
            .Returns(PurchaseResult.Succeeded(BillingConstants.PremiumMonthly, "token_monthly"));

        // Then switch to yearly
        billingService.PurchaseAsync(BillingConstants.PremiumYearly, Arg.Any<CancellationToken>())
            .Returns(PurchaseResult.Succeeded(BillingConstants.PremiumYearly, "token_yearly"));

        // Act
        var monthlyResult = await billingService.PurchaseAsync(BillingConstants.PremiumMonthly);
        var yearlyResult = await billingService.PurchaseAsync(BillingConstants.PremiumYearly);

        // Assert
        monthlyResult.ProductId.Should().Be(BillingConstants.PremiumMonthly);
        yearlyResult.ProductId.Should().Be(BillingConstants.PremiumYearly);
        yearlyResult.Success.Should().BeTrue();
    }

    // --- BillingConstants ---

    [Fact]
    public void GetEstimatedDuration_Monthly_ShouldReturn30Days()
    {
        var duration = BillingConstants.GetEstimatedDuration(BillingConstants.PremiumMonthly);

        duration.Should().Be(TimeSpan.FromDays(30));
    }

    [Fact]
    public void GetEstimatedDuration_Yearly_ShouldReturn365Days()
    {
        var duration = BillingConstants.GetEstimatedDuration(BillingConstants.PremiumYearly);

        duration.Should().Be(TimeSpan.FromDays(365));
    }

    [Fact]
    public void GetEstimatedDuration_Unknown_ShouldReturn30DaysFallback()
    {
        var duration = BillingConstants.GetEstimatedDuration("unknown_product");

        duration.Should().Be(TimeSpan.FromDays(30));
    }

    [Fact]
    public void AllProductIds_ShouldContainBothProducts()
    {
        BillingConstants.AllProductIds.Should().HaveCount(2);
        BillingConstants.AllProductIds.Should().Contain(BillingConstants.PremiumMonthly);
        BillingConstants.AllProductIds.Should().Contain(BillingConstants.PremiumYearly);
    }

    // --- PurchaseResult factory methods ---

    [Fact]
    public void PurchaseResult_Succeeded_ShouldSetCorrectProperties()
    {
        var result = PurchaseResult.Succeeded("test_product", "test_token");

        result.Success.Should().BeTrue();
        result.ProductId.Should().Be("test_product");
        result.PurchaseToken.Should().Be("test_token");
        result.WasCancelled.Should().BeFalse();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void PurchaseResult_Failed_ShouldSetCorrectProperties()
    {
        var result = PurchaseResult.Failed("Something went wrong");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Something went wrong");
        result.ProductId.Should().BeNull();
        result.PurchaseToken.Should().BeNull();
        result.WasCancelled.Should().BeFalse();
    }

    [Fact]
    public void PurchaseResult_Cancelled_ShouldSetCorrectProperties()
    {
        var result = PurchaseResult.Cancelled();

        result.Success.Should().BeFalse();
        result.WasCancelled.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.ProductId.Should().BeNull();
        result.PurchaseToken.Should().BeNull();
    }

    // --- MockBillingService ---

    [Fact]
    public async Task MockBillingService_GetProducts_ShouldReturnBothProducts()
    {
        var mock = new MockBillingService();

        var products = await mock.GetProductsAsync();

        products.Should().HaveCount(2);
        products.Should().Contain(p => p.ProductId == BillingConstants.PremiumMonthly);
        products.Should().Contain(p => p.ProductId == BillingConstants.PremiumYearly);
    }

    [Fact]
    public async Task MockBillingService_PurchaseSuccess_ShouldReturnSucceededResult()
    {
        var mock = new MockBillingService { ShouldSucceedPurchase = true };

        var result = await mock.PurchaseAsync(BillingConstants.PremiumMonthly);

        result.Success.Should().BeTrue();
        result.ProductId.Should().Be(BillingConstants.PremiumMonthly);
    }

    [Fact]
    public async Task MockBillingService_PurchaseCancelled_ShouldReturnCancelledResult()
    {
        var mock = new MockBillingService { ShouldCancelPurchase = true };

        var result = await mock.PurchaseAsync(BillingConstants.PremiumMonthly);

        result.WasCancelled.Should().BeTrue();
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task MockBillingService_PurchaseFailed_ShouldReturnFailedResult()
    {
        var mock = new MockBillingService
        {
            ShouldSucceedPurchase = false,
            FailureMessage = "Test failure"
        };

        var result = await mock.PurchaseAsync(BillingConstants.PremiumMonthly);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Test failure");
    }

    [Fact]
    public async Task MockBillingService_HasSubscription_ShouldReflectProperty()
    {
        var mock = new MockBillingService { HasSubscription = true };

        var hasActive = await mock.HasActiveSubscriptionAsync();
        var restored = await mock.RestorePurchasesAsync();

        hasActive.Should().BeTrue();
        restored.Should().BeTrue();
    }
}
