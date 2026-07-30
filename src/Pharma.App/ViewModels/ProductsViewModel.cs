using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>
/// The medicine catalogue — what a medicine is, not how much of it there is.
///
/// The list and nothing else. Adding or editing opens over the shell, which is
/// what gives the catalogue the full width of the window and gives the form
/// enough room to be filled in without scrolling.
///
/// Stock lives on the Inventory screen. Setting a medicine up happens once;
/// receiving and correcting stock happens every delivery, often by someone else.
/// </summary>
public partial class ProductsViewModel(PharmacyService pharmacy) : ObservableObject, IPage
{
    public string Title => "Medicines";
    public string Subtitle => $"{Products.Count} medicine(s) in the catalogue";

    public ObservableCollection<Product> Products { get; } = [];

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private string _status = "";

    /// <summary>
    /// Which row is highlighted. Nothing is bound to its fields any more — it
    /// only decides what Edit opens.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProduct))]
    private Product? _selectedProduct;

    public bool HasProduct => SelectedProduct is not null;

    public async Task LoadAsync() => await FindAsync();

    [RelayCommand]
    private async Task FindAsync()
    {
        var selectedId = SelectedProduct?.Id;

        Products.Clear();
        foreach (var p in await pharmacy.SearchProductsAsync(Search, 200)) Products.Add(p);

        SelectedProduct = Products.FirstOrDefault(p => p.Id == selectedId);
        OnPropertyChanged(nameof(Subtitle));
    }

    [RelayCommand]
    private Task NewProductAsync() => EditAsync(null);

    /// <summary>Opens whichever row is highlighted. Also what a double-click does.</summary>
    [RelayCommand]
    private Task EditProductAsync()
    {
        if (SelectedProduct is null)
        {
            Status = "Pick a medicine from the list, or choose + New medicine.";
            return Task.CompletedTask;
        }

        return EditAsync(SelectedProduct);
    }

    /// <summary>
    /// Puts the editor over the shell and waits for it to close.
    ///
    /// A fresh view model each time, holding the record it was given. That is
    /// what retired a whole family of bugs on this screen: nothing is shared
    /// between one medicine and the next, so there is no form left half-filled
    /// and no chance of the next medicine being saved over the last one.
    /// </summary>
    private async Task EditAsync(Product? existing)
    {
        var editor = new MedicineEditorViewModel(pharmacy, existing);
        var shell = App.Services.GetRequiredService<MainViewModel>();

        await shell.ShowOverlayAsync(editor, close => editor.RequestClose += () => close());

        // Cancelling says nothing, so the message from whatever happened before
        // is left where it was.
        if (editor.Outcome is { } outcome) Status = outcome;

        await FindAsync();

        // Saving was refused because this medicine already exists and the
        // operator asked to see it. Reopen on the real one.
        if (editor.OpenInstead is { } id)
        {
            var actual = Products.FirstOrDefault(p => p.Id == id);

            if (actual is null)
            {
                // Filtered out by the search box rather than missing.
                Search = "";
                await FindAsync();
                actual = Products.FirstOrDefault(p => p.Id == id);
            }

            if (actual is not null)
            {
                SelectedProduct = actual;
                await EditAsync(actual);
            }
        }
    }
}
