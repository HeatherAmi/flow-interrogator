// MainControl.cs
//
// The actual screen the user sees once Flow Interrogator is open, split into two tabs. The
// Flows tab: a button to load flows, a search box that finds a term across every flow's
// definition, a grid (showing either the full flow list or search results), the selected
// flow's raw JSON, and a flattened grid of its triggers/actions. The Run History tab: loads
// the run history for whichever flow is currently selected on the Flows tab, signing in to the
// separate Power Automate Management API (via MSAL, a system browser popup) the first time
// it's needed. FlowInterrogatorPlugin owns this control and hands it working services once a
// Dataverse connection exists; this control does not know how to connect to either service
// itself, only how to use them once it has them.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using HeatherAmiDigital.FlowInterrogator.Core.Models;
using HeatherAmiDigital.FlowInterrogator.Core.Services;

namespace HeatherAmiDigital.FlowInterrogator.XrmToolBox;

public sealed class MainControl : UserControl
{
    private readonly TabControl _tabs;

    // Flows tab
    private readonly Button _loadButton;
    private readonly Label _statusLabel;
    private readonly TextBox _searchBox;
    private readonly Button _searchButton;
    private readonly Button _resetButton;
    private readonly SplitContainer _splitContainer;
    private readonly DataGridView _grid;
    private readonly SplitContainer _detailSplit;
    private readonly TextBox _definitionBox;
    private readonly DataGridView _actionsGrid;

    // Run History tab
    private readonly Button _loadRunHistoryButton;
    private readonly Label _runHistoryStatusLabel;
    private readonly DataGridView _runHistoryGrid;

    private FlowQueryService _queryService;
    private FlowRunService _runService;
    private IReadOnlyList<FlowSummary> _allFlows = Array.Empty<FlowSummary>();
    private Guid? _selectedFlowId;
    private string _selectedFlowName;

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

        _searchBox = new TextBox
        {
            Top = 45,
            Left = 10,
            Width = 300
        };
        _searchBox.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SearchDefinitions();
            }
        };

        _searchButton = new Button
        {
            Text = "Search definitions",
            Top = 43,
            Left = 320,
            Width = 140
        };
        _searchButton.Click += (s, e) => SearchDefinitions();

        _resetButton = new Button
        {
            Text = "Reset",
            Top = 43,
            Left = 470,
            Width = 80
        };
        _resetButton.Click += (s, e) => ResetSearch();

        _splitContainer = new SplitContainer
        {
            Top = 80,
            Left = 10,
            Width = 900,
            Height = 500,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 200
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

        _detailSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical
        };

        _definitionBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new System.Drawing.Font("Consolas", 9F)
        };

        _actionsGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };

        _detailSplit.Panel1.Controls.Add(_definitionBox);
        _detailSplit.Panel2.Controls.Add(_actionsGrid);

        _splitContainer.Panel1.Controls.Add(_grid);
        _splitContainer.Panel2.Controls.Add(_detailSplit);

        var flowsPanel = new Panel { Dock = DockStyle.Fill };
        flowsPanel.Controls.Add(_splitContainer);
        flowsPanel.Controls.Add(_resetButton);
        flowsPanel.Controls.Add(_searchButton);
        flowsPanel.Controls.Add(_searchBox);
        flowsPanel.Controls.Add(_loadButton);
        flowsPanel.Controls.Add(_statusLabel);

        _loadRunHistoryButton = new Button
        {
            Text = "Load run history",
            Top = 10,
            Left = 10,
            Width = 140
        };
        _loadRunHistoryButton.Click += async (s, e) => await LoadRunHistoryAsync();

        _runHistoryStatusLabel = new Label
        {
            Top = 15,
            Left = 160,
            AutoSize = true,
            Text = "Select a flow on the Flows tab first"
        };

        _runHistoryGrid = new DataGridView
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

        var runHistoryPanel = new Panel { Dock = DockStyle.Fill };
        runHistoryPanel.Controls.Add(_runHistoryGrid);
        runHistoryPanel.Controls.Add(_loadRunHistoryButton);
        runHistoryPanel.Controls.Add(_runHistoryStatusLabel);

        var flowsTab = new TabPage("Flows");
        flowsTab.Controls.Add(flowsPanel);

        var runHistoryTab = new TabPage("Run History");
        runHistoryTab.Controls.Add(runHistoryPanel);

        _tabs = new TabControl { Dock = DockStyle.Fill };
        _tabs.TabPages.AddRange(new[] { flowsTab, runHistoryTab });

        Controls.Add(_tabs);

        Load += (s, e) => _detailSplit.SplitterDistance = _detailSplit.Width / 2;
    }

    public void SetServices(FlowQueryService queryService, FlowRunService runService)
    {
        _queryService = queryService;
        _runService = runService;
        _loadButton.Enabled = queryService != null;
        _statusLabel.Text = queryService != null ? "Connected" : "Not connected";
    }

    private void LoadFlows()
    {
        if (_queryService == null) return;

        try
        {
            _statusLabel.Text = "Loading...";
            _allFlows = _queryService.GetFlowSummaries();
            _grid.DataSource = _allFlows.ToList();
            _statusLabel.Text = $"{_allFlows.Count} flow(s) loaded";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Failed to load flows";
            MessageBox.Show(ex.Message, "Flow Interrogator", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SearchDefinitions()
    {
        if (_queryService == null || string.IsNullOrWhiteSpace(_searchBox.Text)) return;

        try
        {
            _statusLabel.Text = $"Searching for '{_searchBox.Text}'...";
            var matches = _queryService.SearchFlowDefinitions(_searchBox.Text);
            _grid.DataSource = matches.ToList();
            HideColumn("FlowId");
            _statusLabel.Text = $"Found {matches.Count} match(es) for '{_searchBox.Text}'";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Search failed";
            MessageBox.Show(ex.Message, "Flow Interrogator", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ResetSearch()
    {
        _searchBox.Clear();
        _grid.DataSource = _allFlows.ToList();
        _statusLabel.Text = $"{_allFlows.Count} flow(s) loaded";
    }

    private void HideColumn(string columnName)
    {
        if (_grid.Columns.Contains(columnName))
        {
            _grid.Columns[columnName].Visible = false;
        }
    }

    private void ShowSelectedFlowDefinition()
    {
        if (_grid.SelectedRows.Count == 0 || _queryService == null)
        {
            ClearDetail();
            return;
        }

        Guid flowId;
        string flowName;
        switch (_grid.SelectedRows[0].DataBoundItem)
        {
            case FlowSummary summary:
                flowId = summary.Id;
                flowName = summary.Name;
                break;
            case FlowMatch match:
                flowId = match.FlowId;
                flowName = match.FlowName;
                break;
            default:
                ClearDetail();
                return;
        }

        _selectedFlowId = flowId;
        _selectedFlowName = flowName;
        _runHistoryStatusLabel.Text = $"Ready to load run history for '{flowName}'";

        try
        {
            var definition = _queryService.GetFlowDefinition(flowId);
            if (definition == null)
            {
                _definitionBox.Text = "This flow no longer exists in Dataverse.";
                _actionsGrid.DataSource = null;
                return;
            }

            _definitionBox.Text = definition.RawJson?.ToString() ?? string.Empty;
            _actionsGrid.DataSource = BuildActionRows(definition);
        }
        catch (Exception ex)
        {
            _definitionBox.Text = $"Failed to load definition:{Environment.NewLine}{ex.Message}";
            _actionsGrid.DataSource = null;
        }
    }

    private void ClearDetail()
    {
        _selectedFlowId = null;
        _selectedFlowName = null;
        _definitionBox.Text = string.Empty;
        _actionsGrid.DataSource = null;
    }

    private async Task LoadRunHistoryAsync()
    {
        if (_runService == null || _selectedFlowId == null)
        {
            _runHistoryStatusLabel.Text = "Select a flow on the Flows tab first";
            return;
        }

        try
        {
            _runHistoryStatusLabel.Text = "Loading run history (sign in if prompted)...";
            var runs = await _runService.GetRunsAsync(_selectedFlowId.Value, _selectedFlowName);
            _runHistoryGrid.DataSource = runs.ToList();
            _runHistoryStatusLabel.Text = $"{runs.Count} run(s) for '{_selectedFlowName}'";
        }
        catch (Exception ex)
        {
            _runHistoryStatusLabel.Text = "Failed to load run history";
            MessageBox.Show(ex.Message, "Flow Interrogator", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static List<FlowActionRow> BuildActionRows(FlowDefinition definition)
    {
        var rows = new List<FlowActionRow>();

        foreach (var trigger in definition.Triggers.Values)
        {
            rows.Add(new FlowActionRow { Category = "Trigger", Name = trigger.Name, Type = trigger.Type, Kind = trigger.Kind });
        }

        foreach (var action in definition.Actions.Values)
        {
            rows.Add(new FlowActionRow { Category = "Action", Name = action.Name, Type = action.Type, Kind = action.Kind });
        }

        return rows;
    }

    // A flat row for the actions grid. This is a UI-only shape, not a domain concept, so it
    // lives here rather than in Core alongside FlowTrigger/FlowAction.
    private sealed class FlowActionRow
    {
        public string Category { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Kind { get; set; }
    }
}
