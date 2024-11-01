using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

using BizHawk.Client.Common;
using BizHawk.Client.EmuHawk;
using BizHawk.Emulation.Common;

namespace Re3Explorer;

[ExternalTool("RE3 Explorer")]
// currently only SLPM-87224 supported
[ExternalToolApplicability.SingleRom(VSystemID.Raw.PSX, GameVersionSlpm87224.Hash)]
public sealed class Re3Explorer: ToolFormBase, IExternalToolForm {
    private struct CallStats {
        public uint FrameCalls = 0;
        public uint RoomCalls = 0;
        public uint TotalCalls = 0;

        public CallStats() { }
    }
    
    private const ushort InitialRngState = 0x6CA4;

    public ApiContainer? MaybeApi { get; set; }

    private ApiContainer Api => MaybeApi!;

    private readonly GameVersions _gameVersions = new();

    private GameVersion? _version;

    private CallStats _randCalls;
    private CallStats _scriptCalls;
    private readonly List<(int, IList<uint>)> _callHistory = [];

    private bool _trackingPatchesApplied;
    private string _lastRoom = "";
    private bool _framerateToggle;
    private int _numCallRows = 20;
    private int _numCallColumns = 2;

    private readonly Font _font = new(FontFamily.GenericSansSerif, 13);
    private readonly Label _roomLabel;
    private readonly Label _rngValueLabel;
    private readonly Label _rngFrameCallsLabel;
    private readonly Label _rngRoomCallsLabel;
    private readonly Label _rngTotalCallsLabel;
    private readonly Label _scriptRngValueLabel;
    private readonly Label _scriptRngOffsetIndexLabel;
    private readonly Label _scriptRngOffsetLabel;
    private readonly TableLayoutPanel _callsTable;
    private readonly List<List<Label>> _callLabels = [];

    protected override string WindowTitleStatic => "RE3 Explorer";

    public Re3Explorer() {
        Shown += OnShow;
        
        ClientSize = new Size(480, 320);
        SuspendLayout();
        BackColor = Color.FromArgb(0xEC, 0xE9, 0xD8);

        var root = new FlowLayoutPanel {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
        };

        _roomLabel = new Label { AutoSize = true, Font = _font, Text = "Room: N/A" };
        
        var rngTable = new TableLayoutPanel {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
        };
        
        // header
        rngTable.Controls.Add(new Label { AutoSize = true, Font = _font, Text = "Value" }, 1, 0);
        rngTable.Controls.Add(new Label { AutoSize = true, Font = _font, Text = "Frame" }, 2, 0);
        rngTable.Controls.Add(new Label { AutoSize = true, Font = _font, Text = "Room" }, 3, 0);
        rngTable.Controls.Add(new Label { AutoSize = true, Font = _font, Text = "Total" }, 4, 0);
        
        // RNG
        rngTable.Controls.Add(new Label { Anchor = AnchorStyles.Right, AutoSize = true, Font = _font, Text = "RNG" }, 0, 1);
        _rngValueLabel = new Label { AutoSize = true, Font = _font, Text = "" };
        rngTable.Controls.Add(_rngValueLabel, 1, 1);
        _rngFrameCallsLabel = new Label { AutoSize = true, Font = _font, Text = "" };
        rngTable.Controls.Add(_rngFrameCallsLabel, 2, 1);
        _rngRoomCallsLabel = new Label { AutoSize = true, Font = _font, Text = "" };
        rngTable.Controls.Add(_rngRoomCallsLabel, 3, 1);
        _rngTotalCallsLabel = new Label { AutoSize = true, Font = _font, Text = "" };
        rngTable.Controls.Add(_rngTotalCallsLabel, 4, 1);
        
        // script RNG
        rngTable.Controls.Add(new Label { Anchor = AnchorStyles.Right, AutoSize = true, Font = _font, Text = "Script RNG" }, 0, 2);
        _scriptRngValueLabel = new Label { AutoSize = true, Font = _font, Text = "" };
        rngTable.Controls.Add(_scriptRngValueLabel, 1, 2);
        
        rngTable.Controls.Add(new Label { Anchor = AnchorStyles.Right, AutoSize = true, Font = _font, Text = "Script RNG offset index" }, 0, 3);
        _scriptRngOffsetIndexLabel = new Label { AutoSize = true, Font = _font, Text = "" };
        rngTable.Controls.Add(_scriptRngOffsetIndexLabel, 1, 3);
        rngTable.Controls.Add(new Label { AutoSize = true, Font = _font, Text = "N/A" }, 2, 3);
        rngTable.Controls.Add(new Label { AutoSize = true, Font = _font, Text = "N/A" }, 3, 3);
        rngTable.Controls.Add(new Label { AutoSize = true, Font = _font, Text = "N/A" }, 4, 3);
        
        rngTable.Controls.Add(new Label { Anchor = AnchorStyles.Right, AutoSize = true, Font = _font, Text = "Script RNG offset" }, 0, 4);
        _scriptRngOffsetLabel = new Label { AutoSize = true, Font = _font, Text = "" };
        rngTable.Controls.Add(_scriptRngOffsetLabel, 1, 4);
        rngTable.Controls.Add(new Label { AutoSize = true, Font = _font, Text = "N/A" }, 2, 4);
        rngTable.Controls.Add(new Label { AutoSize = true, Font = _font, Text = "N/A" }, 3, 4);
        rngTable.Controls.Add(new Label { AutoSize = true, Font = _font, Text = "N/A" }, 4, 4);

        _callsTable = new TableLayoutPanel {
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            BackColor = Color.WhiteSmoke,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
        };
        
        _callsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        for (var i = 1; i < _numCallColumns; i++) {
            _callsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        }
        
        // generate and insert labels
        for (var row = 0; row < _numCallRows; row++) {
            _callsTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            var labelList = new List<Label>();
            for (var column = 0; column < _numCallColumns; column++) {
                var callLabel = new Label { AutoSize = true, Font = _font, Text = "" };
                _callsTable.Controls.Add(callLabel, column, row);
                labelList.Add(callLabel);
            }
            
            _callLabels.Add(labelList);
        }
        
        root.Controls.Add(_roomLabel);
        root.Controls.Add(rngTable);
        root.Controls.Add(new Label { AutoSize = true, Font = _font, Text = "Calls:" });
        root.Controls.Add(_callsTable);
        
        Controls.Add(root);
        
        ResumeLayout(false);
        PerformLayout();
    }

    private void OnShow(object sender, EventArgs e) {
        var parentPosition = Owner.PointToScreen(Point.Empty);
        Top = parentPosition.Y;
        Left = parentPosition.X + Owner.Width;
        Height = Owner.Height;
    }

    private void UpdateStats() {
        if (_version is null || !_trackingPatchesApplied) {
            return;
        }

        var randCalls = _version.GetRandCalls();
        var numRandCalls = (uint)randCalls.Count;
        _randCalls.FrameCalls = numRandCalls;
        _randCalls.RoomCalls += numRandCalls;
        _randCalls.TotalCalls += numRandCalls;
        
        _callHistory.Add((Api.Emulation.FrameCount(), randCalls));
    }

    public override void Restart() {
        // TODO: register event listeners
        if (Api.Emulation.GetGameInfo() is { } gameInfo) {
            _version = _gameVersions.Get(gameInfo.Hash, Api.Memory);
        } else {
            _version = null;
        }

        _trackingPatchesApplied = false;
        _lastRoom = "";
        _randCalls = new();
        _scriptCalls = new();
        _callHistory.Clear();
    }

    protected override void UpdateAfter() {
        if (_version is null) {
            return;
        }

        var currentRoom = _version.RoomId;
        if (currentRoom != _lastRoom) {
            _lastRoom = currentRoom;
            _randCalls.RoomCalls = 0;
            _scriptCalls.RoomCalls = 0;
        }

        if (_trackingPatchesApplied) {
            UpdateStats();
        } else if (_version.RngState == InitialRngState) {
            _version.ApplyRandTrackingPatch();
            _trackingPatchesApplied = true;
        }
        
        SuspendLayout();
        
        _roomLabel.Text = $"Room: {currentRoom}";
        _rngValueLabel.Text = $"{_version.RngState:X04}";
        _scriptRngValueLabel.Text = $"{_version.ScriptRngState:X04}";
        
        var scriptRngOffsetIndex = _version.ScriptRngOffsetIndex;
        var scriptRngOffset = _version.ScriptRngOffset;
        _scriptRngOffsetIndexLabel.Text = $"{scriptRngOffsetIndex} ({scriptRngOffsetIndex:X02})";
        _scriptRngOffsetLabel.Text = $"{scriptRngOffset} ({scriptRngOffset:X02})";

        // because gameplay runs at 30fps, don't show a 0 value unless it happens two frames in a row
        if (_randCalls.FrameCalls == 0 && !_framerateToggle) {
            _framerateToggle = true;
        } else {
            _rngFrameCallsLabel.Text = $"{_randCalls.FrameCalls}";
            _framerateToggle = false;
        }

        _rngRoomCallsLabel.Text = $"{_randCalls.RoomCalls}";
        _rngTotalCallsLabel.Text = $"{_randCalls.TotalCalls}";
        
        // re-populate call history table
        var rowIndex = 0;
        for (var historyIndex = _callHistory.Count - 1; historyIndex >= 0 && rowIndex < _numCallRows; historyIndex--) {
            var (frame, calls) = _callHistory[historyIndex];
            if (calls.Count == 0) {
                continue;
            }
            
            var labels = _callLabels[rowIndex];
            labels[0].Text = $"{frame}";
            for (var i = 1; i < labels.Count || i <= calls.Count; i++) {
                if (i < labels.Count) {
                    var label = labels[i];
                    
                    if (i <= calls.Count) {
                        label.Text = $"{calls[i - 1]:X08}";
                    } else if (label.Text != "") {
                        label.Text = "";
                    } else {
                        break;
                    }
                } else {
                    var label = new Label { AutoSize = true, Font = _font, Text = $"{calls[i - 1]:X08}" };
                    labels.Add(label);

                    if (i >= _callsTable.ColumnStyles.Count) {
                        _callsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
                    }
                    
                    _callsTable.Controls.Add(label, i, rowIndex);
                }
            }

            ++rowIndex;
        }

        // clear any unused labels
        for (; rowIndex < _numCallRows; rowIndex++) {
            foreach (var label in _callLabels[rowIndex]) {
                if (label.Text == "") {
                    break; // everything past this point will be blank
                }
                label.Text = "";
            }
        }
        
        ResumeLayout();
    }
}