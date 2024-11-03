using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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
    private const int NumCharacters = 15;

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

    private readonly Font _font = new(FontFamily.GenericSansSerif, 13);
    private readonly Label _roomLabel;
    private readonly Label _rngValueHexLabel;
    private readonly Label _rngValueDecLabel;
    private readonly Label _rngFrameCallsLabel;
    private readonly Label _rngRoomCallsLabel;
    private readonly Label _rngTotalCallsLabel;
    private readonly Label _scriptRngValueHexLabel;
    private readonly Label _scriptRngValueDecLabel;
    private readonly Label _scriptFrameCallsLabel;
    private readonly Label _scriptRoomCallsLabel;
    private readonly Label _scriptTotalCallsLabel;
    private readonly Label _scriptRngOffsetIndexHexLabel;
    private readonly Label _scriptRngOffsetIndexDecLabel;
    private readonly Label _scriptRngOffsetHexLabel;
    private readonly Label _scriptRngOffsetDecLabel;

    private readonly TableLayoutPanel _characterTable;
    private readonly List<Label[]> _characterLabels = [];
    private readonly CheckBox _showCallsCheckBox;
    
    private readonly CallTablePanel _callTablePanel;

    protected override string WindowTitleStatic => "RE3 Explorer";

    public Re3Explorer() {
        Shown += OnShow;
        
        ClientSize = new Size(1200, 600);
        SuspendLayout();
        BackColor = Color.FromArgb(0xEC, 0xE9, 0xD8);

        var root = new TableLayoutPanel {
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            Dock = DockStyle.Fill,
        };
        
        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _roomLabel = NewLabel("Room: N/A");
        
        var watchTable = new TableLayoutPanel {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
        };
        
        // header
        watchTable.Controls.Add(NewLabel("Hex"), 1, 0);
        watchTable.Controls.Add(NewLabel("Dec"), 2, 0);
        watchTable.Controls.Add(NewLabel("Frame"), 3, 0);
        watchTable.Controls.Add(NewLabel("Room"), 4, 0);
        watchTable.Controls.Add(NewLabel("Total"), 5, 0);
        
        // RNG
        watchTable.Controls.Add(NewLabel("RNG", AnchorStyles.Right), 0, 1);
        _rngValueHexLabel = NewLabel();
        watchTable.Controls.Add(_rngValueHexLabel, 1, 1);
        _rngValueDecLabel = NewLabel();
        watchTable.Controls.Add(_rngValueDecLabel, 2, 1);
        _rngFrameCallsLabel = NewLabel();
        watchTable.Controls.Add(_rngFrameCallsLabel, 3, 1);
        _rngRoomCallsLabel = NewLabel();
        watchTable.Controls.Add(_rngRoomCallsLabel, 4, 1);
        _rngTotalCallsLabel = NewLabel();
        watchTable.Controls.Add(_rngTotalCallsLabel, 5, 1);
        
        // script RNG
        watchTable.Controls.Add(NewLabel("Script RNG", AnchorStyles.Right), 0, 2);
        _scriptRngValueHexLabel = NewLabel();
        watchTable.Controls.Add(_scriptRngValueHexLabel, 1, 2);
        _scriptRngValueDecLabel = NewLabel();
        watchTable.Controls.Add(_scriptRngValueDecLabel, 2, 2);
        _scriptFrameCallsLabel = NewLabel();
        watchTable.Controls.Add(_scriptFrameCallsLabel, 3, 2);
        _scriptRoomCallsLabel = NewLabel();
        watchTable.Controls.Add(_scriptRoomCallsLabel, 4, 2);
        _scriptTotalCallsLabel = NewLabel();
        watchTable.Controls.Add(_scriptTotalCallsLabel, 5, 2);
        
        watchTable.Controls.Add(NewLabel("Script RNG offset index", AnchorStyles.Right), 0, 3);
        _scriptRngOffsetIndexHexLabel = NewLabel();
        watchTable.Controls.Add(_scriptRngOffsetIndexHexLabel, 1, 3);
        _scriptRngOffsetIndexDecLabel = NewLabel();
        watchTable.Controls.Add(_scriptRngOffsetIndexDecLabel, 2, 3);
        
        watchTable.Controls.Add(NewLabel("Script RNG offset", AnchorStyles.Right), 0, 4);
        _scriptRngOffsetHexLabel = NewLabel();
        watchTable.Controls.Add(_scriptRngOffsetHexLabel, 1, 4);
        _scriptRngOffsetDecLabel = NewLabel();
        watchTable.Controls.Add(_scriptRngOffsetDecLabel, 2, 4);

        _characterTable = new TableLayoutPanel {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
        };

        _characterTable.Controls.Add(NewLabel("X"), 1, 0);
        _characterTable.Controls.Add(NewLabel("Y"), 2, 0);
        _characterTable.Controls.Add(NewLabel("Z"), 3, 0);
        _characterTable.Controls.Add(NewLabel("Health"), 4, 0);

        for (var i = 0; i < NumCharacters; i++) {
            AddCharacterRow(i);
        }

        _callTablePanel = new CallTablePanel(_callHistory) {
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right | AnchorStyles.Left,
            BackColor = Color.WhiteSmoke,
            Font = _font,
        };
        
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(_roomLabel);
        
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(watchTable);
        
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(NewLabel("Characters"));
        
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(_characterTable);

        _showCallsCheckBox = new CheckBox {
            AutoSize = true,
            Font = _font,
            Text = "Calls",
        };
        root.Controls.Add(_showCallsCheckBox, 1, 0);
        root.Controls.Add(_callTablePanel, 1, 1);
        root.SetRowSpan(_callTablePanel, 3);
        
        Controls.Add(root);
        
        ResumeLayout(false);
        PerformLayout();
    }
    
    private Label NewLabel(string text = "", AnchorStyles anchor = AnchorStyles.Left) => new() { Anchor = anchor, AutoSize = true, Font = _font, Text = text };

    private void AddCharacterRow(int i) {
        Label[] labels =
            [NewLabel(i == 0 ? "Player" : $"Enemy {i}"), NewLabel(), NewLabel(), NewLabel(), NewLabel()];
        for (var j = 0; j < labels.Length; j++) {
            _characterTable.Controls.Add(labels[j], j, i + 1);
        }
        _characterLabels.Add(labels);
    }

    private void SetCharacterStats(int i, GameVersion.Character? character = null) {
        while (i >= _characterLabels.Count) {
            AddCharacterRow(i);
        }
        
        var labels = _characterLabels[i];
        if (character is null) {
            labels[1].Text = "";
            labels[2].Text = "";
            labels[3].Text = "";
            labels[4].Text = "";
        } else {
            labels[1].Text = $"{character.X}";
            labels[2].Text = $"{character.Y}";
            labels[3].Text = $"{character.Z}";
            labels[4].Text = $"{character.Health}";
        }
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

        var numScriptCalls = (uint)randCalls.Count(call => (call & 0x3FFFFF) == _version.ScriptRandAddress);
        _scriptCalls.FrameCalls = numScriptCalls;
        _scriptCalls.RoomCalls += numScriptCalls;
        _scriptCalls.TotalCalls += numScriptCalls;

        if (numRandCalls > 0) {
            _callTablePanel.Add(Api.Emulation.FrameCount(), randCalls);
        }
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
        
        SuspendLayout();
        
        _callTablePanel.DrawEnabled = _showCallsCheckBox.Checked;

        if (_trackingPatchesApplied) {
            UpdateStats();
        } else if (_version.RngState == InitialRngState) {
            _version.ApplyRandTrackingPatch();
            _trackingPatchesApplied = true;
        }
        
        _roomLabel.Text = $"Room: {currentRoom}";
        _rngValueHexLabel.Text = $"{_version.RngState:X04}";
        _rngValueDecLabel.Text = $"{_version.RngState}";
        _scriptRngValueHexLabel.Text = $"{_version.ScriptRngState:X04}";
        _scriptRngValueDecLabel.Text = $"{_version.ScriptRngState}";
        
        var scriptRngOffsetIndex = _version.ScriptRngOffsetIndex;
        var scriptRngOffset = _version.ScriptRngOffset;
        _scriptRngOffsetIndexHexLabel.Text = $"{scriptRngOffsetIndex:X02}";
        _scriptRngOffsetIndexDecLabel.Text = $"{scriptRngOffsetIndex}";
        _scriptRngOffsetHexLabel.Text = $"{scriptRngOffset:X02}";
        _scriptRngOffsetDecLabel.Text = $"{scriptRngOffset}";

        // because gameplay runs at 30fps, don't show a 0 value unless it happens two frames in a row
        if (_randCalls.FrameCalls == 0 && !_framerateToggle) {
            _framerateToggle = true;
        } else {
            _rngFrameCallsLabel.Text = $"{_randCalls.FrameCalls}";
            _scriptFrameCallsLabel.Text = $"{_scriptCalls.FrameCalls}";
            _framerateToggle = false;
        }

        _rngRoomCallsLabel.Text = $"{_randCalls.RoomCalls}";
        _rngTotalCallsLabel.Text = $"{_randCalls.TotalCalls}";
        
        _scriptRoomCallsLabel.Text = $"{_scriptCalls.RoomCalls}";
        _scriptTotalCallsLabel.Text = $"{_scriptCalls.TotalCalls}";

        SetCharacterStats(0, _version.Player);
        var enemies = _version.Enemies;
        for (var i = 1; i < NumCharacters; i++) {
            SetCharacterStats(i, i < enemies.Count ? enemies[i] : null);
        }
        
        ResumeLayout();
    }
}