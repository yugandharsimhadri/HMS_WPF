using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.Core;

namespace Pharma.App.ViewModels;

/// <summary>
/// Picks one procedure and a quantity to add to the current bill — the
/// popup form behind Pediatrics' (and later Dentist's) "+ Add procedure"
/// button, matching "billing for procedures... as popup and addition."
/// </summary>
public partial class AddProcedureLineViewModel : ObservableObject
{
    public AddProcedureLineViewModel(IEnumerable<Procedure> procedures)
    {
        foreach (var p in procedures) Procedures.Add(p);
    }

    public string Header => "Add procedure";

    public event Action? RequestClose;
    public ProcedureBillRow? Result { get; private set; }

    public ObservableCollection<Procedure> Procedures { get; } = [];
    [ObservableProperty] private Procedure? _selectedProcedure;
    [ObservableProperty] private int _quantity = 1;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _procedureMissing;

    partial void OnSelectedProcedureChanged(Procedure? value)
    {
        if (value is not null) ProcedureMissing = false;
    }

    [RelayCommand]
    private void Add()
    {
        if (SelectedProcedure is not { } procedure)
        {
            ProcedureMissing = true;
            Status = "Pick a procedure.";
            return;
        }

        Result = new ProcedureBillRow
        {
            ProcedureId = procedure.Id, ProcedureName = procedure.Name, Price = procedure.Price,
            Quantity = Math.Max(1, Quantity), Kind = ProcedureBillRowKind.Procedure
        };
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke();
}
