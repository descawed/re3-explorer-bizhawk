using System;
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

    private bool _trackingPatchesApplied;
    private string _lastRoom = "";
    private bool _framerateToggle;

    private readonly Label _roomLabel;
    private readonly Label _rngValueLabel;
    private readonly Label _rngFrameCallsLabel;
    private readonly Label _rngRoomCallsLabel;
    private readonly Label _rngTotalCallsLabel;
    private readonly Label _scriptRngValueLabel;
    private readonly Label _scriptRngOffsetIndexLabel;
    private readonly Label _scriptRngOffsetLabel;

    protected override string WindowTitleStatic => "RE3 Explorer";

    public Re3Explorer() {
        Shown += OnShow;
        
        ClientSize = new Size(480, 320);
        SuspendLayout();
        BackColor = Color.LightGray;

        var font = new Font(FontFamily.GenericSansSerif, 13);

        var root = new FlowLayoutPanel {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
        };

        _roomLabel = new Label { AutoSize = true, Font = font, Text = "Room: N/A" };
        
        var table = new TableLayoutPanel {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
        };
        
        // header
        table.Controls.Add(new Label { AutoSize = true, Font = font, Text = "Value" }, 1, 0);
        table.Controls.Add(new Label { AutoSize = true, Font = font, Text = "Frame" }, 2, 0);
        table.Controls.Add(new Label { AutoSize = true, Font = font, Text = "Room" }, 3, 0);
        table.Controls.Add(new Label { AutoSize = true, Font = font, Text = "Total" }, 4, 0);
        
        // RNG
        table.Controls.Add(new Label { Anchor = AnchorStyles.Right, AutoSize = true, Font = font, Text = "RNG" }, 0, 1);
        _rngValueLabel = new Label { AutoSize = true, Font = font, Text = "" };
        table.Controls.Add(_rngValueLabel, 1, 1);
        _rngFrameCallsLabel = new Label { AutoSize = true, Font = font, Text = "" };
        table.Controls.Add(_rngFrameCallsLabel, 2, 1);
        _rngRoomCallsLabel = new Label { AutoSize = true, Font = font, Text = "" };
        table.Controls.Add(_rngRoomCallsLabel, 3, 1);
        _rngTotalCallsLabel = new Label { AutoSize = true, Font = font, Text = "" };
        table.Controls.Add(_rngTotalCallsLabel, 4, 1);
        
        // script RNG
        table.Controls.Add(new Label { Anchor = AnchorStyles.Right, AutoSize = true, Font = font, Text = "Script RNG" }, 0, 2);
        _scriptRngValueLabel = new Label { AutoSize = true, Font = font, Text = "" };
        table.Controls.Add(_scriptRngValueLabel, 1, 2);
        
        table.Controls.Add(new Label { Anchor = AnchorStyles.Right, AutoSize = true, Font = font, Text = "Script RNG offset index" }, 0, 3);
        _scriptRngOffsetIndexLabel = new Label { AutoSize = true, Font = font, Text = "" };
        table.Controls.Add(_scriptRngOffsetIndexLabel, 1, 3);
        table.Controls.Add(new Label { AutoSize = true, Font = font, Text = "N/A" }, 2, 3);
        table.Controls.Add(new Label { AutoSize = true, Font = font, Text = "N/A" }, 3, 3);
        table.Controls.Add(new Label { AutoSize = true, Font = font, Text = "N/A" }, 4, 3);
        
        table.Controls.Add(new Label { Anchor = AnchorStyles.Right, AutoSize = true, Font = font, Text = "Script RNG offset" }, 0, 4);
        _scriptRngOffsetLabel = new Label { AutoSize = true, Font = font, Text = "" };
        table.Controls.Add(_scriptRngOffsetLabel, 1, 4);
        table.Controls.Add(new Label { AutoSize = true, Font = font, Text = "N/A" }, 2, 4);
        table.Controls.Add(new Label { AutoSize = true, Font = font, Text = "N/A" }, 3, 4);
        table.Controls.Add(new Label { AutoSize = true, Font = font, Text = "N/A" }, 4, 4);
        
        root.Controls.Add(_roomLabel);
        root.Controls.Add(table);
        
        Controls.Add(root);
        
        ResumeLayout(false);
        PerformLayout();
    }

    private void OnShow(object sender, EventArgs e) {
        var parentPosition = Owner.PointToScreen(Point.Empty);
        Top = parentPosition.Y;
        Left = parentPosition.X + Owner.Width;
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
    }

    public override void Restart() {
        // TODO: reset stats and register event listeners
        if (Api.Emulation.GetGameInfo() is { } gameInfo) {
            _version = _gameVersions.Get(gameInfo.Hash, Api.Memory);
        } else {
            _version = null;
        }

        _trackingPatchesApplied = false;
        _lastRoom = "";
        _randCalls = new();
        _scriptCalls = new();
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
    }
}