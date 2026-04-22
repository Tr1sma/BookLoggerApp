using BookLoggerApp.Core.Entitlements;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Unit.Entitlements;

public class PaywallComparisonCatalogTests
{
    [Fact]
    public void Rows_is_not_empty()
    {
        PaywallComparisonCatalog.Rows.Should().NotBeEmpty();
    }

    [Fact]
    public void Every_category_has_at_least_one_row()
    {
        var categoriesInRows = PaywallComparisonCatalog.Rows
            .Select(r => r.Category)
            .Distinct()
            .ToHashSet();

        foreach (PaywallComparisonCatalog.Category category in Enum.GetValues<PaywallComparisonCatalog.Category>())
        {
            categoriesInRows.Should().Contain(category, $"category {category} must have at least one comparison row");
        }
    }

    [Fact]
    public void No_row_has_all_three_cells_empty()
    {
        foreach (var row in PaywallComparisonCatalog.Rows)
        {
            bool allEmpty = string.IsNullOrWhiteSpace(row.FreeValue)
                         && string.IsNullOrWhiteSpace(row.PlusValue)
                         && string.IsNullOrWhiteSpace(row.PremiumValue);
            allEmpty.Should().BeFalse($"row '{row.Label}' must have at least one non-empty cell");
        }
    }

    [Fact]
    public void Rows_are_grouped_by_category_in_order()
    {
        var seenCategories = new List<PaywallComparisonCatalog.Category>();
        PaywallComparisonCatalog.Category? previousCategory = null;

        foreach (var row in PaywallComparisonCatalog.Rows)
        {
            if (previousCategory is null || previousCategory != row.Category)
            {
                seenCategories.Should().NotContain(row.Category,
                    $"category {row.Category} appears again after a different category — rows must stay grouped");
                seenCategories.Add(row.Category);
            }
            previousCategory = row.Category;
        }
    }

    [Fact]
    public void Mapped_feature_keys_match_their_row_values()
    {
        foreach (var row in PaywallComparisonCatalog.Rows)
        {
            if (row.MappedFeature is null)
            {
                continue;
            }

            SubscriptionTier minimum = FeaturePolicy.GetMinimumTier(row.MappedFeature.Value);

            // If feature requires Premium, the Plus column must be a "—" (not available).
            if (minimum == SubscriptionTier.Premium)
            {
                row.PlusValue.Should().Be("—",
                    $"row '{row.Label}' maps to a Premium-only feature but Plus column is not '—'");
                row.PremiumValue.Should().NotBe("—",
                    $"row '{row.Label}' maps to a Premium-only feature but Premium column shows '—'");
            }
        }
    }

    [Fact]
    public void GetCategoryLabel_returns_human_readable_strings()
    {
        foreach (PaywallComparisonCatalog.Category category in Enum.GetValues<PaywallComparisonCatalog.Category>())
        {
            string label = PaywallComparisonCatalog.GetCategoryLabel(category);
            label.Should().NotBeNullOrWhiteSpace($"category {category} must have a label");
        }
    }
}
