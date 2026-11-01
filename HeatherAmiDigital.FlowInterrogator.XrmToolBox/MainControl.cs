// MainControl.cs
//
// The actual screen the user sees once Flow Interrogator is open: a button to load
// flows and a grid to show them in. FlowInterrogatorPlugin owns this control and hands
// it a working FlowQueryService once a Dataverse connection exists. This control does
// not know how to connect to Dataverse itself, only how to use a service once it has
// one, that split keeps connection handling and screen layout separate.

using System;
using System.Linq;
using System.Windows.Forms;
using HeatherAmiDigital.FlowInterrogator.Core.Services;

namespace HeatherAmiDigital.FlowInterrogator.XrmToolBox;

public sealed class MainControl : UserControl
{
    private readonly Button _loadButton;
    private readonly DataGridView _grid;
    private readonly Label _statusLabel;

    private FlowQueryService _queryService;

    public MainControl()
    {
        Dock = DockStyle.Fill;

        _loadButton = new Button
        {
            Text = "Load flows",
            Top = 10,
            Left = 10,
            Width = 100,
            Enabled = false
        };
        _loadButton.Click += (s, e) => LoadFlows();

        _statusLabel = new Label
        {
            Top = 15,
            Left = 120,
            AutoSize = true,
            Text = "Not connected"
        };

        _grid = new DataGridView
        {
            Top = 45,
            Left = 10,
            Width = 900,
            Height = 500,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };

        Controls.Add(_grid);
        Controls.Add(_loadButton);
        Controls.Add(_statusLabel);
    }

    public void SetQueryService(FlowQueryService queryService)
    {
        _queryService = queryService;
        _loadButton.Enabled = queryService != null;
        _statusLabel.Text = queryService != null ? "Connected" : "Not connected";
    }

    private void LoadFlows()
    {
        if (_queryService == null) return;

        try
        {
            _statusLabel.Text = "Loading...";
            var flows = _queryService.GetFlowSummaries().ToList();
            _grid.DataSource = flows;
            _statusLabel.Text = $"{flows.Count} flow(s) loaded";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Failed to load flows";
            MessageBox.Show(ex.Message, "Flow Interrogator", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
