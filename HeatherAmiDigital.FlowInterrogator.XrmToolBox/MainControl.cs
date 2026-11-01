// MainControl.cs
//
// The actual screen the user sees once Flow Interrogator is open: a button to load
// flows, a status label, a grid of flows, and underneath it a read-only box showing the
// raw JSON definition of whichever flow is currently selected in the grid.
// FlowInterrogatorPlugin owns this control and hands it a working FlowQueryService once a
// Dataverse connection exists. This control does not know how to connect to Dataverse
// itself, only how to use a service once it has one, that split keeps connection handling
// and screen layout separate.

using System;
using System.Linq;
using System.Windows.Forms;
using HeatherAmiDigital.FlowInterrogator.Core.Models;
using HeatherAmiDigital.FlowInterrogator.Core.Services;

namespace HeatherAmiDigital.FlowInterrogator.XrmToolBox;

public sealed class MainControl : UserControl
{
    private readonly Button _loadButton;
    private readonly Label _statusLabel;
    private readonly SplitContainer _splitContainer;
    private readonly DataGridView _grid;
    private readonly TextBox _definitionBox;

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

        _splitContainer = new SplitContainer
        {
            Top = 45,
            Left = 10,
            Width = 900,
            Height = 500,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 300
        };

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        _grid.SelectionChanged += (s, e) => ShowSelectedFlowDefinition();

        _definitionBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new System.Drawing.Font("Consolas", 9F)
        };

        _splitContainer.Panel1.Controls.Add(_grid);
        _splitContainer.Panel2.Controls.Add(_definitionBox);

        Controls.Add(_splitContainer);
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

    private void ShowSelectedFlowDefinition()
    {
        if (_grid.SelectedRows.Count == 0 || _queryService == null)
        {
            _definitionBox.Text = string.Empty;
            return;
        }

        var flow = _grid.SelectedRows[0].DataBoundItem as FlowSummary;
        if (flow == null)
        {
            _definitionBox.Text = string.Empty;
            return;
        }

        try
        {
            _definitionBox.Text = _queryService.GetFlowDefinitionJson(flow.Id);
        }
        catch (Exception ex)
        {
            _definitionBox.Text = $"Failed to load definition:{Environment.NewLine}{ex.Message}";
        }
    }
}
