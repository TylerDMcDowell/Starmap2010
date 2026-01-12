using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using StarMap2010.Models;
using StarMap2010.Data;

namespace StarMap2010
{
    //MainForm.Core

    public partial class MainForm : Form
    {
        // -----------------------------
        // Core UI infrastructure
        // -----------------------------
        private MenuStrip menu;
        private Panel infoPanel;
        private Panel viewport;
        private MapCanvas canvas;
        private ToolTip toolTip;

        // Menu items (owned by Menu.cs)
        private ToolStripMenuItem mnuTools;
        private ToolStripMenuItem mnuGovernments;
        //private ToolStripMenuItem mnuView;
        private ToolStripMenuItem mnuShowJumpGates;
        private ToolStripMenuItem mnuFilters;
        private ToolStripMenuItem mnuFilterGovernments;

        private StarMap2010.Ui.CollapsibleSection _secGates;
        private StarMap2010.Ui.CollapsibleSection _secContents;
        private Panel _sidebar;


        // -----------------------------
        // Sidebar UI
        // -----------------------------
        private Label lblSysName;
        private Label lblGov;              // repurposed: "Gov • system_type"
        private Label lblPrimary;
        private Label lblType;
        private Label lblCoords;
        private Label lblPrimaryStarType;
        private Label lblNotes;

        private SplitContainer splitContents;
        private Panel pnlDetailsHost;


        //private Label lblGateInfo;

        private Button btnNotes;
        private Label lblGateSummary;

        private RichTextBox rtbGateInfo;
        private Button btnEditGates;

        // ---- System Contents Tree ----
        private Label lblSystemContents;
        private TreeView tvSystemObjects;

        // -----------------------------
        // Data / state
        // -----------------------------
        private readonly string _dbPath;

        private readonly StarSystemsDao _systemsDao;
        private readonly JumpGatesDao _gatesDao;
        private GovernmentsDao _govDao;

        private readonly SystemObjectsDao _objDao;
        private readonly NotesDao _notesDao;

        private readonly List<StarSystemInfo> systems = new List<StarSystemInfo>();
        private List<StarSystemInfo> visibleSystems = new List<StarSystemInfo>();

        private StarSystemInfo selectedA;
        private StarSystemInfo selectedB;
        private string selectedIdA;
        private string selectedIdB;

        // Current system shown in the left panel
        private StarSystemInfo _currentSelectedForSidebar;

        private readonly Dictionary<string, JumpGate> gateBySystemId =
            new Dictionary<string, JumpGate>(StringComparer.Ordinal);

        private readonly Dictionary<string, JumpGate> gateByGateId =
            new Dictionary<string, JumpGate>(StringComparer.Ordinal);

        private readonly List<JumpGateRenderableLink> gateLinks =
            new List<JumpGateRenderableLink>();

        // -----------------------------
        // View constants
        // -----------------------------
        private float zoomFactor = 1f;

        private const int CANVAS_SIZE = 14000;
        private const float SCALE_A = 18.0f;

        private const float ZOOM_MIN = 0.3f;
        private const float ZOOM_MAX = 12.0f;
        private const float ZOOM_STEP = 0.2f;

        private const int CLICK_SLOP_PX = 4;

        // -----------------------------
        // Input / scroll state
        // -----------------------------
        private bool spaceDown;
        private bool isPanning;
        private bool mouseDownWasSpace;
        private int lastPanTick;

        private Point mouseDownCanvas;
        private Point panMouseDown;
        private Point panScrollDown;

        private float desiredScrollX;
        private float desiredScrollY;
        private bool desiredScrollInit;

        // -----------------------------
        // ctor
        // -----------------------------
        public MainForm(string dbPath)
        {
            _dbPath = dbPath;

            if (!System.IO.File.Exists(_dbPath))
            {
                MessageBox.Show("Database not found:\r\n" + _dbPath, "StarMap2010",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            _systemsDao = new StarSystemsDao(_dbPath);
            _gatesDao = new JumpGatesDao(_dbPath);
            _govDao = new GovernmentsDao(_dbPath);

            _objDao = new SystemObjectsDao(_dbPath);
            _notesDao = new NotesDao(_dbPath);

            this.Text = "StarMap2010";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.WindowState = FormWindowState.Maximized;
            this.MinimumSize = new Size(900, 650);

            BuildUi();
            ReloadAndRefresh();

            this.Shown += (s, e) =>
            {
                CenterViewportOn(CANVAS_SIZE / 2, CANVAS_SIZE / 2);
                canvas.Focus();
            };
        }

        // -----------------------------
        // helpers
        // -----------------------------
        private ToolTip EnsureToolTip()
        {
            if (toolTip == null)
                toolTip = new ToolTip();
            return toolTip;
        }
    }
}
