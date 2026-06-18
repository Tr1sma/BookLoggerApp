using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Infrastructure;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Unit.ViewModels;

public class BlindDateViewModelTests
{
    private readonly IBlindDateService _service;
    private readonly BlindDateViewModel _vm;

    public BlindDateViewModelTests()
    {
        DatabaseInitializationHelper.MarkAsInitialized();
        _service = Substitute.For<IBlindDateService>();
        _service.GetCandidatesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Book>>(new List<Book>()));
        _vm = new BlindDateViewModel(_service);
    }

    private static Book BookWith(string title, string[]? tropes = null, string[]? genres = null)
    {
        var book = new Book { Id = Guid.NewGuid(), Title = title, Author = "Author" };

        if (tropes is not null)
        {
            foreach (var name in tropes)
            {
                book.BookTropes.Add(new BookTrope
                {
                    BookId = book.Id,
                    TropeId = Guid.NewGuid(),
                    Trope = new Trope { Name = name }
                });
            }
        }

        if (genres is not null)
        {
            foreach (var name in genres)
            {
                book.BookGenres.Add(new BookGenre
                {
                    BookId = book.Id,
                    GenreId = Guid.NewGuid(),
                    Genre = new Genre { Name = name }
                });
            }
        }

        return book;
    }

    private void SetupCandidates(params Book[] books)
    {
        _service.GetCandidatesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Book>>(books.ToList()));
    }

    [Fact]
    public async Task LoadAsync_PopulatesCards_WhenCandidatesExist()
    {
        SetupCandidates(
            BookWith("A", tropes: new[] { "Slow Burn" }),
            BookWith("B", tropes: new[] { "Mafia" }));

        await _vm.LoadCommand.ExecuteAsync(null);

        _vm.HasCandidates.Should().BeTrue();
        _vm.ShownCards.Should().HaveCount(2);
    }

    [Fact]
    public async Task LoadAsync_LimitsShownCardsToThree()
    {
        SetupCandidates(
            BookWith("A"), BookWith("B"), BookWith("C"), BookWith("D"), BookWith("E"));

        await _vm.LoadCommand.ExecuteAsync(null);

        _vm.ShownCards.Should().HaveCount(3);
    }

    [Fact]
    public async Task LoadAsync_SetsHasCandidatesFalse_WhenEmpty()
    {
        await _vm.LoadCommand.ExecuteAsync(null);

        _vm.HasCandidates.Should().BeFalse();
        _vm.ShownCards.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_UsesTropeNamesAsVibes()
    {
        SetupCandidates(BookWith("A", tropes: new[] { "Enemies to Lovers", "Slow Burn" }));

        await _vm.LoadCommand.ExecuteAsync(null);

        var card = _vm.ShownCards.Single();
        card.IsGenreFallback.Should().BeFalse();
        card.Vibes.Should().BeEquivalentTo("Enemies to Lovers", "Slow Burn");
    }

    [Fact]
    public async Task LoadAsync_FallsBackToGenre_WhenNoTropes()
    {
        SetupCandidates(BookWith("A", genres: new[] { "Fantasy" }));

        await _vm.LoadCommand.ExecuteAsync(null);

        var card = _vm.ShownCards.Single();
        card.IsGenreFallback.Should().BeTrue();
        card.Vibes.Should().ContainSingle().Which.Should().Be("Fantasy");
    }

    [Fact]
    public async Task LoadAsync_CapsVibesAtFour()
    {
        SetupCandidates(BookWith("A", tropes: new[]
        {
            "T1", "T2", "T3", "T4", "T5", "T6"
        }));

        await _vm.LoadCommand.ExecuteAsync(null);

        _vm.ShownCards.Single().Vibes.Should().HaveCount(4);
    }

    [Fact]
    public async Task LoadAsync_UsesMysteryVibe_WhenNoTropesAndNoGenres()
    {
        SetupCandidates(BookWith("A"));

        await _vm.LoadCommand.ExecuteAsync(null);

        var card = _vm.ShownCards.Single();
        card.IsGenreFallback.Should().BeTrue();
        // No tropes/genres → one localized fallback vibe.
        card.Vibes.Should().ContainSingle().Which.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Reveal_SetsRevealedBookAndFlag()
    {
        SetupCandidates(BookWith("A", tropes: new[] { "Slow Burn" }));
        await _vm.LoadCommand.ExecuteAsync(null);
        var card = _vm.ShownCards.Single();

        _vm.Reveal(card);

        _vm.HasRevealed.Should().BeTrue();
        _vm.RevealedBook.Should().BeSameAs(card.Book);
    }

    [Fact]
    public async Task PickAnother_ClearsReveal()
    {
        SetupCandidates(BookWith("A"));
        await _vm.LoadCommand.ExecuteAsync(null);
        _vm.Reveal(_vm.ShownCards.Single());

        _vm.PickAnother();

        _vm.HasRevealed.Should().BeFalse();
        _vm.RevealedBook.Should().BeNull();
    }

    [Fact]
    public void Reveal_Ignores_Null()
    {
        _vm.Reveal(null);

        _vm.HasRevealed.Should().BeFalse();
        _vm.RevealedBook.Should().BeNull();
    }

    [Fact]
    public async Task Reveal_SetsAValidRevealAnimation()
    {
        SetupCandidates(BookWith("A"));
        await _vm.LoadCommand.ExecuteAsync(null);

        _vm.Reveal(_vm.ShownCards.Single());

        _vm.RevealAnimation.Should().BeOneOf(BlindDateRevealAnimation.Unwrap, BlindDateRevealAnimation.Burst);
    }

    [Fact]
    public async Task Reveal_RandomlyUsesBothAnimationVariants()
    {
        SetupCandidates(BookWith("A"));
        await _vm.LoadCommand.ExecuteAsync(null);
        var card = _vm.ShownCards.Single();

        var seen = new HashSet<BlindDateRevealAnimation>();
        for (int i = 0; i < 60; i++)
        {
            _vm.Reveal(card);
            seen.Add(_vm.RevealAnimation);
        }

        // Both variants essentially certain over 60 draws.
        seen.Should().Contain(new[] { BlindDateRevealAnimation.Unwrap, BlindDateRevealAnimation.Burst });
    }
}
