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
    private readonly Label _rngValueLabel;
    private readonly Label _rngFrameCallsLabel;
    private readonly Label _rngRoomCallsLabel;
    private readonly Label _rngTotalCallsLabel;
    private readonly Label _scriptRngValueLabel;
    private readonly Label _scriptFrameCallsLabel;
    private readonly Label _scriptRoomCallsLabel;
    private readonly Label _scriptTotalCallsLabel;
    private readonly Label _scriptRngOffsetIndexLabel;
    private readonly Label _scriptRngOffsetLabel;
    
    private readonly CallTablePanel _callTablePanel;

    protected override string WindowTitleStatic => "RE3 Explorer";

    public Re3Explorer() {
        Shown += OnShow;
        
        ClientSize = new Size(800, 320);
        SuspendLayout();
        BackColor = Color.FromArgb(0xEC, 0xE9, 0xD8);

        var root = new TableLayoutPanel {
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            Dock = DockStyle.Fill,
        };
        
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

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
        _scriptFrameCallsLabel = new Label { AutoSize = true, Font = _font, Text = "" };
        rngTable.Controls.Add(_scriptFrameCallsLabel, 2, 2);
        _scriptRoomCallsLabel = new Label { AutoSize = true, Font = _font, Text = "" };
        rngTable.Controls.Add(_scriptRoomCallsLabel, 3, 2);
        _scriptTotalCallsLabel = new Label { AutoSize = true, Font = _font, Text = "" };
        rngTable.Controls.Add(_scriptTotalCallsLabel, 4, 2);
        
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

        _callTablePanel = new CallTablePanel(_callHistory) {
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.WhiteSmoke,
            Font = _font,
        };
        
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(_roomLabel, 0, 0);
        
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(rngTable);
        
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(new Label { AutoSize = true, Font = _font, Text = "Calls:" });

        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(_callTablePanel);
        
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

        if (_trackingPatchesApplied) {
            UpdateStats();
        } else if (_version.RngState == InitialRngState) {
            _version.ApplyRandTrackingPatch();
            _trackingPatchesApplied = true;
        }
        
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
            _scriptFrameCallsLabel.Text = $"{_scriptCalls.FrameCalls}";
            _framerateToggle = false;
        }

        _rngRoomCallsLabel.Text = $"{_randCalls.RoomCalls}";
        _rngTotalCallsLabel.Text = $"{_randCalls.TotalCalls}";
        
        _scriptRoomCallsLabel.Text = $"{_scriptCalls.RoomCalls}";
        _scriptTotalCallsLabel.Text = $"{_scriptCalls.TotalCalls}";
        
        ResumeLayout();
    }
}