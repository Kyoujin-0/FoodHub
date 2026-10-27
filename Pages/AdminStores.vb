Option Strict Off
Option Infer On
Imports System.Drawing
Imports System.Windows.Forms
Imports System.Collections.Generic
Imports System.Drawing.Drawing2D
Imports System.Linq
Imports Timer = System.Windows.Forms.Timer

' =====================================================================
' Pages/AdminStores.vb : the admin "Stores" tab
'   Map / List toggle, overview card, stall details, vacant slot (place a stall), Move to another slot,
'   Close/Open stall, Edit layout. There is NO "Archive stall": deactivate the seller in the Sellers tab
'   (the stall then leaves the map and its slot becomes vacant).
' A plain Panel with its own layout (like SellerInboxPage), not a PageBase.
' Event RecordPaymentRequested(stallId): the host should switch to the Rent tab.
' =====================================================================

Public NotInheritable Class StoreStyle
    Private Sub New()
    End Sub
    Public Shared ReadOnly Ink As Color = Color.FromArgb(43, 27, 18)
    Public Shared ReadOnly Muted As Color = Color.FromArgb(122, 106, 94)
    Public Shared ReadOnly Accent As Color = Color.FromArgb(255, 107, 53)
    Public Shared ReadOnly AccentDark As Color = Color.FromArgb(214, 78, 28)
    Public Shared ReadOnly AccentSoft As Color = Color.FromArgb(255, 236, 227)
    Public Shared ReadOnly PageBg As Color = Theme.Bg
    Public Shared ReadOnly Line As Color = Color.FromArgb(236, 226, 214)
    Public Shared ReadOnly Soft As Color = Color.FromArgb(250, 245, 238)
    Public Shared ReadOnly Green As Color = Color.FromArgb(52, 179, 109)
    Public Shared ReadOnly Amber As Color = Color.FromArgb(240, 164, 58)
    Public Shared ReadOnly Red As Color = Color.FromArgb(220, 84, 70)

    Public Shared Function Peso(v As Decimal) As String
        Return ChrW(&H20B1) & v.ToString("N0")
    End Function

    Public Shared Function RentText(st As FloorStall) As String
        Select Case st.RentStatus
            Case "paid" : Return "Paid"
            Case "partial" : Return "Partly paid"
            Case "overdue" : Return "Overdue"
            Case Else : Return "Due soon"
        End Select
    End Function

    Public Shared Function RentFore(status As String) As Color
        Select Case status
            Case "paid" : Return Color.FromArgb(31, 138, 84)
            Case "overdue" : Return Color.FromArgb(190, 55, 45)
            Case Else : Return Color.FromArgb(154, 106, 0)
        End Select
    End Function

    Public Shared Function RentBack(status As String) As Color
        Select Case status
            Case "paid" : Return Color.FromArgb(226, 246, 236)
            Case "overdue" : Return Color.FromArgb(253, 229, 226)
            Case Else : Return Color.FromArgb(253, 240, 213)
        End Select
    End Function

    Public Shared Function RoundPath(r As RectangleF, radius As Single) As GraphicsPath
        Dim p As New GraphicsPath()
        Dim d = Math.Max(1.0F, Math.Min(radius * 2, Math.Min(r.Width, r.Height)))
        p.AddArc(r.X, r.Y, d, d, 180, 90)
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90)
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90)
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90)
        p.CloseFigure()
        Return p
    End Function

    Public Shared Function Lbl(text As String, size As Single, bold As Boolean, col As Color) As Label
        Return New Label With {.Text = text, .Font = Theme.F(size, bold), .ForeColor = col, .AutoSize = False, .BackColor = Color.Transparent}
    End Function

    ''' <summary>Sets the label width and a height that fits the wrapped text.</summary>
    Public Shared Sub Fit(l As Label, width As Integer)
        Dim sz = TextRenderer.MeasureText(If(l.Text, ""), l.Font, New Size(width, 5000), TextFormatFlags.WordBreak)
        l.Width = width
        l.Height = Math.Max(sz.Height + 4, 18)
    End Sub
End Class

' ---------------------------------------------------------------- small painted controls

Public Class StoreButton
    Inherits Control
    Public Property Kind As Integer = 1          ' 0 orange, 1 outline, 2 soft orange, 3 red outline
    Private _hover As Boolean

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        Cursor = Cursors.Hand
        Font = Theme.F(10.5F, True)
        Height = 44
    End Sub

    Public Sub FitWidth(Optional pad As Integer = 40)
        Width = TextRenderer.MeasureText(Text, Font).Width + pad
    End Sub

    Protected Overrides Sub OnMouseEnter(e As EventArgs)
        MyBase.OnMouseEnter(e)
        _hover = True
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        _hover = False
        Invalidate()
    End Sub

    Protected Overrides Sub OnEnabledChanged(e As EventArgs)
        MyBase.OnEnabledChanged(e)
        Cursor = If(Enabled, Cursors.Hand, Cursors.Default)
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.Clear(If(Parent IsNot Nothing, Parent.BackColor, Color.White))
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim fill, border, fore As Color
        Select Case Kind
            Case 0
                fill = If(_hover, StoreStyle.AccentDark, StoreStyle.Accent) : border = fill : fore = Color.White
            Case 2
                fill = If(_hover, Color.FromArgb(255, 226, 212), StoreStyle.AccentSoft) : border = fill : fore = StoreStyle.AccentDark
            Case 3
                fill = If(_hover, Color.FromArgb(254, 244, 242), Color.White) : border = Color.FromArgb(240, 200, 195) : fore = StoreStyle.Red
            Case Else
                fill = If(_hover, Color.FromArgb(252, 247, 242), Color.White) : border = StoreStyle.Line : fore = StoreStyle.Ink
        End Select
        If Not Enabled Then
            fill = Color.FromArgb(244, 240, 236) : border = fill : fore = Color.FromArgb(170, 160, 150)
        End If
        Using p = StoreStyle.RoundPath(New RectangleF(0.5F, 0.5F, Width - 2, Height - 2), CSng((Height - 2) / 2))
            Using br As New SolidBrush(fill)
                g.FillPath(br, p)
            End Using
            Using pen As New Pen(border, 1)
                g.DrawPath(pen, p)
            End Using
        End Using
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, fore, TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis)
    End Sub
End Class

Public Class StorePill
    Inherits Control
    Public Property Fore As Color = StoreStyle.Ink
    Public Property Back As Color = Color.White
    Public Property Bordered As Boolean = False

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        Font = Theme.F(10.0F, True)
        Height = 28
    End Sub

    Public Sub FitWidth(Optional pad As Integer = 26)
        Width = TextRenderer.MeasureText(Text, Font).Width + pad
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.Clear(If(Parent IsNot Nothing, Parent.BackColor, Color.White))
        g.SmoothingMode = SmoothingMode.AntiAlias
        Using p = StoreStyle.RoundPath(New RectangleF(0.5F, 0.5F, Width - 2, Height - 2), CSng((Height - 2) / 2))
            Using br As New SolidBrush(Back)
                g.FillPath(br, p)
            End Using
            If Bordered Then
                Using pen As New Pen(StoreStyle.Line, 1)
                    g.DrawPath(pen, p)
                End Using
            End If
        End Using
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, Fore, TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine)
    End Sub
End Class

Public Class StoreSegment
    Inherits Control
    Public Event SelectedChanged()
    Private _items() As String = {}
    Private _index As Integer = 0

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        Cursor = Cursors.Hand
        Font = Theme.F(11.0F, True)
        Height = 50
    End Sub

    Public Sub SetItems(items() As String)
        _items = items
        Width = items.Sum(Function(t) TextRenderer.MeasureText(t, Font).Width + 44) + 12
        Invalidate()
    End Sub

    Public Property SelectedIndex As Integer
        Get
            Return _index
        End Get
        Set(value As Integer)
            If value = _index Then Return
            _index = value
            Invalidate()
            RaiseEvent SelectedChanged()
        End Set
    End Property

    Private Function CellRect(i As Integer) As Rectangle
        Dim x As Integer = 6
        For k = 0 To i - 1
            x += TextRenderer.MeasureText(_items(k), Font).Width + 44
        Next
        Return New Rectangle(x, 6, TextRenderer.MeasureText(_items(i), Font).Width + 44, Height - 12)
    End Function

    Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
        MyBase.OnMouseClick(e)
        For i = 0 To _items.Length - 1
            If CellRect(i).Contains(e.Location) Then
                SelectedIndex = i
                Return
            End If
        Next
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.Clear(If(Parent IsNot Nothing, Parent.BackColor, StoreStyle.PageBg))
        g.SmoothingMode = SmoothingMode.AntiAlias
        Using p = StoreStyle.RoundPath(New RectangleF(0.5F, 0.5F, Width - 2, Height - 2), CSng((Height - 2) / 2))
            g.FillPath(Brushes.White, p)
            Using pen As New Pen(StoreStyle.Line, 1)
                g.DrawPath(pen, p)
            End Using
        End Using
        For i = 0 To _items.Length - 1
            Dim r = CellRect(i)
            If i = _index Then
                Using p = StoreStyle.RoundPath(New RectangleF(r.X, r.Y, r.Width, r.Height), CSng(r.Height / 2))
                    Using br As New SolidBrush(StoreStyle.AccentSoft)
                        g.FillPath(br, p)
                    End Using
                End Using
            End If
            TextRenderer.DrawText(g, _items(i), Font, r, If(i = _index, StoreStyle.Accent, StoreStyle.Muted),
                                  TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine)
        Next
    End Sub
End Class

Public Class StoreStat
    Inherits Control
    Public Property Number As String = "0"
    Public Property Caption As String = ""
    Public Property Dot As Color = StoreStyle.Green

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.Clear(If(Parent IsNot Nothing, Parent.BackColor, Color.White))
        g.SmoothingMode = SmoothingMode.AntiAlias
        Using p = StoreStyle.RoundPath(New RectangleF(0, 0, Width - 1, Height - 1), 16)
            Using br As New SolidBrush(StoreStyle.Soft)
                g.FillPath(br, p)
            End Using
        End Using
        TextRenderer.DrawText(g, Number, Theme.F(26.0F, True), New Rectangle(18, 10, Width - 30, 44), StoreStyle.Ink, TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine)
        Using br As New SolidBrush(Dot)
            g.FillEllipse(br, 20, Height - 33, 11, 11)
        End Using
        TextRenderer.DrawText(g, Caption, Theme.F(11.0F, True), New Rectangle(38, Height - 40, Width - 44, 26), StoreStyle.Muted, TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine)
    End Sub
End Class

Public Class StoreLogo
    Inherits Control
    Public Property Picture As Image
    Public Property Initial As String = "?"

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        Size = New Size(64, 64)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.Clear(If(Parent IsNot Nothing, Parent.BackColor, Color.White))
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.InterpolationMode = InterpolationMode.HighQualityBicubic
        Using p = StoreStyle.RoundPath(New RectangleF(0, 0, Width - 1, Height - 1), 16)
            If Picture IsNot Nothing Then
                g.SetClip(p)
                Dim s = Math.Min(Picture.Width, Picture.Height)
                Dim src As New Rectangle((Picture.Width - s) \ 2, (Picture.Height - s) \ 2, s, s)
                g.DrawImage(Picture, New Rectangle(0, 0, Width, Height), src, GraphicsUnit.Pixel)
                g.ResetClip()
            Else
                Using br As New SolidBrush(StoreStyle.AccentSoft)
                    g.FillPath(br, p)
                End Using
                TextRenderer.DrawText(g, Initial, Theme.F(20.0F, True), ClientRectangle, StoreStyle.Accent, TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine)
            End If
        End Using
    End Sub
End Class

Public Class StoreSoftPanel
    Inherits Panel
    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw Or ControlStyles.SupportsTransparentBackColor, True)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.Clear(If(Parent IsNot Nothing, Parent.BackColor, Color.White))
        g.SmoothingMode = SmoothingMode.AntiAlias
        Using p = StoreStyle.RoundPath(New RectangleF(0, 0, Width - 1, Height - 1), 18)
            Using br As New SolidBrush(StoreStyle.Soft)
                g.FillPath(br, p)
            End Using
        End Using
    End Sub
End Class

Public Class StoreCardPanel
    Inherits Panel
    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        Padding = New Padding(8)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.Clear(StoreStyle.PageBg)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Using p = StoreStyle.RoundPath(New RectangleF(0, 0, Width - 1, Height - 1), 22)
            g.FillPath(Brushes.White, p)
            Using pen As New Pen(Color.FromArgb(244, 236, 226), 1)
                g.DrawPath(pen, p)
            End Using
        End Using
    End Sub
End Class

' ---------------------------------------------------------------- the page

Public Class AdminStoresPage
    Inherits Panel

    Public Event RecordPaymentRequested(stallId As Integer)

    Private Const Mx As Integer = 56
    Private Const TopY As Integer = 88

    ' state
    Private _layout As New FloorLayout()
    Private _draft As FloorLayout
    Private _origSig As String = ""
    Private _stalls As New List(Of FloorStall)
    Private _sig As String = ""
    Private _mode As String = "view"            ' view | pick | edit
    Private _view As String = "overview"        ' overview | stall | vacant (what the side card shows)
    Private _selStallId As Integer
    Private _selSlot As Integer
    Private _pickStallId As Integer
    Private _syncing As Boolean
    Private _contentH As Integer = 400
    Private _flash As String = ""
    Private ReadOnly _logos As New Dictionary(Of String, Image)

    ' header
    Private _title As Label
    Private _err As Label
    Private _editBtn As StoreButton
    Private _countPill As StorePill
    Private _seg As StoreSegment

    ' body
    Private _map As FloorMap3D
    Private _listCard As StoreCardPanel
    Private _list As ListView
    Private _card As StoreCardPanel
    Private _pOverview As Panel
    Private _pStall As Panel
    Private _pVacant As Panel
    Private _pEdit As Panel
    Private WithEvents _poll As New Timer() With {.Interval = 10000}

    ' overview panel
    Private _ovTitle, _ovSub, _ovHint, _ovFlash As Label
    Private _stPaid, _stUnpaid, _stOver, _stVacant As StoreStat

    ' stall panel
    Private _spLogo As StoreLogo
    Private _spName, _spSub, _spSellerCap, _spSellerVal, _spRentCap, _spRentAmt, _spRentInfo, _spNote As Label
    Private _spPillRent, _spPillOpen As StorePill
    Private _spRent As StoreSoftPanel
    Private _spRecord, _spToggle, _spMove As StoreButton

    ' vacant panel
    Private _vpTitle, _vpCap, _vpEmpty, _vpHint As Label
    Private _vpPill As StorePill
    Private _vpCombo As ComboBox
    Private _vpPlace As StoreButton

    ' edit panel
    Private _edTitle, _edHint, _edFloorCap, _edSel, _edErr As Label
    Private _edCapW, _edCapH, _edCapX, _edCapY, _edCapSW, _edCapSH, _edCapFace As Label
    Private _nFloorW, _nFloorH, _nX, _nY, _nW, _nH As NumericUpDown
    Private _cFace As ComboBox
    Private _btnAdd, _btnRemove, _btnSave, _btnCancel As StoreButton
    Private _edFooter As Panel
    Private ReadOnly _faces() As String = {"n", "e", "s", "w"}

    Public Sub New()
        BackColor = StoreStyle.PageBg
        DoubleBuffered = True
        BuildUi()
        Try
            LoadAll()
            _map.ShowOverview(False)
            ShowOverviewPanel()
        Catch ex As Exception
            _err.Text = "Could not load the stores: " & ex.Message
            _err.Visible = True
        End Try
        DoLayout()
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            _poll.Stop()
            _poll.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub

    ''' <summary>Reload everything from the database (the host may call this when the tab is shown again).</summary>
    Public Sub RefreshData()
        If _mode = "edit" Then Return
        Try
            ReloadStalls()
        Catch
        End Try
    End Sub

    ' =============================================================== build

    Private Sub BuildUi()
        _title = StoreStyle.Lbl("Stores", 30.0F, True, StoreStyle.Ink)
        _err = StoreStyle.Lbl("", 11.0F, False, StoreStyle.Red)
        _err.Visible = False
        _editBtn = New StoreButton With {.Text = "Edit layout", .Kind = 1, .Height = 46}
        _editBtn.FitWidth(54)
        _countPill = New StorePill With {.Text = "0 / 0", .Fore = StoreStyle.Ink, .Back = Color.White, .Bordered = True, .Height = 46, .Font = Theme.F(11.5F, True)}
        _seg = New StoreSegment()
        _seg.SetItems({"Map", "List"})
        Controls.AddRange(New Control() {_title, _err, _editBtn, _countPill, _seg})

        _map = New FloorMap3D() With {.CanvasBack = StoreStyle.PageBg}
        _listCard = New StoreCardPanel() With {.Visible = False}
        _listCard.Padding = New Padding(14)
        _list = New ListView() With {.View = View.Details, .FullRowSelect = True, .HideSelection = False, .MultiSelect = False,
                                     .BorderStyle = BorderStyle.None, .BackColor = Color.White, .Font = Theme.F(11.0F, False),
                                     .HeaderStyle = ColumnHeaderStyle.Nonclickable, .Dock = DockStyle.Fill}
        _list.Columns.Add("Slot", 70)
        _list.Columns.Add("Stall", 220)
        _list.Columns.Add("Seller", 150)
        _list.Columns.Add("Category", 140)
        _list.Columns.Add("Rent this month", 170)
        _list.Columns.Add("Status", 100)
        _listCard.Controls.Add(_list)

        _card = New StoreCardPanel()
        _pOverview = NewSidePanel()
        _pStall = NewSidePanel()
        _pVacant = NewSidePanel()
        _pEdit = NewSidePanel()
        _pEdit.AutoScroll = True
        _card.Controls.AddRange(New Control() {_pOverview, _pStall, _pVacant, _pEdit})
        Controls.AddRange(New Control() {_map, _listCard, _card})

        BuildOverview()
        BuildStall()
        BuildVacant()
        BuildEdit()

        AddHandler Resize, Sub(s, e) DoLayout()
        AddHandler _editBtn.Click, Sub(s, e) BeginEdit()
        AddHandler _seg.SelectedChanged, AddressOf OnSegChanged
        AddHandler _map.SlotClicked, AddressOf OnMapSlotClicked
        AddHandler _map.BackClicked, AddressOf OnMapBack
        AddHandler _map.ViewReset, Sub() If _mode = "view" Then ShowOverviewPanel()
        AddHandler _map.EditSelectionChanged, Sub() SyncEditFields()
        AddHandler _map.EditShapeChanged, Sub() SyncEditFields()
        AddHandler _list.SelectedIndexChanged, AddressOf OnListSelect
    End Sub

    Private Function NewSidePanel() As Panel
        Dim p As New Panel() With {.Dock = DockStyle.Fill, .BackColor = Color.White, .Visible = False}
        Return p
    End Function

    Private Sub BuildOverview()
        _ovTitle = StoreStyle.Lbl("Overview", 22.0F, True, StoreStyle.Ink)
        _ovSub = StoreStyle.Lbl("", 12.0F, False, StoreStyle.Muted)
        _stPaid = New StoreStat With {.Caption = "Paid", .Dot = StoreStyle.Green}
        _stUnpaid = New StoreStat With {.Caption = "Unpaid", .Dot = StoreStyle.Amber}
        _stOver = New StoreStat With {.Caption = "Overdue", .Dot = StoreStyle.Red}
        _stVacant = New StoreStat With {.Caption = "Vacant", .Dot = Color.FromArgb(200, 190, 180)}
        _ovFlash = StoreStyle.Lbl("", 11.5F, True, StoreStyle.AccentDark)
        _ovHint = StoreStyle.Lbl("Select a stall on the map to see its details, or a vacant slot to place one.", 11.5F, False, StoreStyle.Muted)
        _pOverview.Controls.AddRange(New Control() {_ovTitle, _ovSub, _stPaid, _stUnpaid, _stOver, _stVacant, _ovFlash, _ovHint})
    End Sub

    Private Sub BuildStall()
        _spLogo = New StoreLogo()
        _spName = StoreStyle.Lbl("", 19.0F, True, StoreStyle.Ink)
        _spSub = StoreStyle.Lbl("", 12.0F, False, StoreStyle.Muted)
        _spPillRent = New StorePill()
        _spPillOpen = New StorePill()
        _spSellerCap = StoreStyle.Lbl("Seller", 12.0F, False, StoreStyle.Muted)
        _spSellerVal = StoreStyle.Lbl("", 12.0F, True, StoreStyle.Ink)
        _spSellerVal.TextAlign = ContentAlignment.TopRight
        _spRent = New StoreSoftPanel() With {.BackColor = StoreStyle.Soft}
        _spRentCap = StoreStyle.Lbl("Rent this month", 11.5F, False, StoreStyle.Muted)
        _spRentAmt = StoreStyle.Lbl("", 24.0F, True, StoreStyle.Ink)
        _spRentInfo = StoreStyle.Lbl("", 11.0F, False, StoreStyle.Muted)
        _spRecord = New StoreButton With {.Text = "Record payment", .Kind = 0, .Height = 40}
        _spRent.Controls.AddRange(New Control() {_spRentCap, _spRentAmt, _spRentInfo, _spRecord})
        _spToggle = New StoreButton With {.Text = "Close stall", .Kind = 1}
        _spMove = New StoreButton With {.Text = "Move to another slot", .Kind = 1}
        _spNote = StoreStyle.Lbl("To take this stall off the map, deactivate its seller in the Sellers tab.", 10.5F, False, StoreStyle.Muted)
        _pStall.Controls.AddRange(New Control() {_spLogo, _spName, _spSub, _spPillRent, _spPillOpen, _spSellerCap, _spSellerVal, _spRent, _spToggle, _spMove, _spNote})
        AddHandler _spRecord.Click, Sub(s, e) RaiseEvent RecordPaymentRequested(_selStallId)
        AddHandler _spToggle.Click, AddressOf OnToggleOpen
        AddHandler _spMove.Click, AddressOf OnMoveClick
    End Sub

    Private Sub BuildVacant()
        _vpTitle = StoreStyle.Lbl("Slot", 22.0F, True, StoreStyle.Ink)
        _vpPill = New StorePill With {.Text = "Vacant", .Fore = Color.FromArgb(120, 108, 96), .Back = Color.FromArgb(242, 237, 231)}
        _vpPill.FitWidth()
        _vpCap = StoreStyle.Lbl("Place a stall here", 12.0F, True, StoreStyle.Ink)
        _vpCombo = New ComboBox() With {.DropDownStyle = ComboBoxStyle.DropDownList, .Font = Theme.F(11.5F, False), .FlatStyle = FlatStyle.Flat}
        _vpPlace = New StoreButton With {.Text = "Place stall", .Kind = 0}
        _vpEmpty = StoreStyle.Lbl("No approved stall is waiting for a slot. New sellers appear here after you approve them in the Sellers tab.", 11.5F, False, StoreStyle.Muted)
        _vpHint = StoreStyle.Lbl("Use Edit layout to move, resize or remove this slot.", 10.5F, False, StoreStyle.Muted)
        _pVacant.Controls.AddRange(New Control() {_vpTitle, _vpPill, _vpCap, _vpCombo, _vpPlace, _vpEmpty, _vpHint})
        AddHandler _vpPlace.Click, AddressOf OnPlaceClick
    End Sub

    Private Function NewNum(min As Integer, max As Integer) As NumericUpDown
        Dim n As New NumericUpDown() With {.Minimum = min, .Maximum = max, .Increment = 10, .Font = Theme.F(11.0F, False), .ThousandsSeparator = False}
        AddHandler n.ValueChanged, AddressOf OnEditNumeric
        AddHandler n.KeyPress, AddressOf OnNumKey
        Return n
    End Function

    Private Sub OnNumKey(sender As Object, e As KeyPressEventArgs)
        If Char.IsControl(e.KeyChar) OrElse Char.IsDigit(e.KeyChar) Then Return
        e.Handled = True
    End Sub

    Private Sub BuildEdit()
        _edTitle = StoreStyle.Lbl("Edit layout", 22.0F, True, StoreStyle.Ink)
        _edHint = StoreStyle.Lbl("Drag a slot to move it and drag its handles to resize. Everything snaps to a 10-unit grid. The orange edge is the open side facing the dining area.", 11.0F, False, StoreStyle.Muted)
        _edFloorCap = StoreStyle.Lbl("Floor size", 11.5F, True, StoreStyle.Ink)
        _edCapW = StoreStyle.Lbl("Width", 10.0F, False, StoreStyle.Muted)
        _edCapH = StoreStyle.Lbl("Height", 10.0F, False, StoreStyle.Muted)
        _nFloorW = NewNum(200, 3000)
        _nFloorH = NewNum(200, 3000)
        _edSel = StoreStyle.Lbl("", 12.0F, True, StoreStyle.Ink)
        _edCapX = StoreStyle.Lbl("Left", 10.0F, False, StoreStyle.Muted)
        _edCapY = StoreStyle.Lbl("Top", 10.0F, False, StoreStyle.Muted)
        _edCapSW = StoreStyle.Lbl("Width", 10.0F, False, StoreStyle.Muted)
        _edCapSH = StoreStyle.Lbl("Height", 10.0F, False, StoreStyle.Muted)
        _nX = NewNum(0, 3000)
        _nY = NewNum(0, 3000)
        _nW = NewNum(FloorMap3D.MinSize, 3000)
        _nH = NewNum(FloorMap3D.MinSize, 3000)
        _edCapFace = StoreStyle.Lbl("Open side (faces the dining area)", 10.0F, False, StoreStyle.Muted)
        _cFace = New ComboBox() With {.DropDownStyle = ComboBoxStyle.DropDownList, .Font = Theme.F(11.0F, False), .FlatStyle = FlatStyle.Flat}
        _cFace.Items.AddRange({"Top edge", "Right edge", "Bottom edge", "Left edge"})
        AddHandler _cFace.SelectedIndexChanged, AddressOf OnEditNumeric
        _btnAdd = New StoreButton With {.Text = "Add slot", .Kind = 2, .Height = 40}
        _btnRemove = New StoreButton With {.Text = "Remove slot", .Kind = 3, .Height = 40}
        _edErr = StoreStyle.Lbl("", 10.5F, True, StoreStyle.Red)
        _btnCancel = New StoreButton With {.Text = "Cancel", .Kind = 1, .Height = 44}
        _btnSave = New StoreButton With {.Text = "Save layout", .Kind = 0, .Height = 44}
        _pEdit.Controls.AddRange(New Control() {_edTitle, _edHint, _edFloorCap, _edCapW, _edCapH, _nFloorW, _nFloorH, _edSel,
                                  _edCapX, _edCapY, _edCapSW, _edCapSH, _nX, _nY, _nW, _nH, _edCapFace, _cFace,
                                  _btnAdd, _btnRemove})
        _edFooter = New Panel() With {.Dock = DockStyle.Bottom, .Height = 76, .BackColor = Color.White, .Visible = False}
        _edFooter.Controls.AddRange(New Control() {_edErr, _btnCancel, _btnSave})
        _card.Controls.Add(_edFooter)
        AddHandler _btnAdd.Click, Sub(s, e) AddSlot()
        AddHandler _btnRemove.Click, Sub(s, e) RemoveSlot()
        AddHandler _btnSave.Click, Sub(s, e) SaveEdit()
        AddHandler _btnCancel.Click, Sub(s, e) CancelEdit()
    End Sub

    ' =============================================================== layout

    Private Sub DoLayout()
        If Width < 300 OrElse Height < 200 Then Return
        Dim right = Width - 52
        _title.SetBounds(Mx, 14, 360, 56)
        _err.SetBounds(Mx, 70, Width - Mx * 2, 22)

        Dim x As Integer = right - _seg.Width
        _seg.Location = New Point(x, 16)
        x -= 14 + 112
        _countPill.SetBounds(x, 18, 112, 46)
        x -= 14 + _editBtn.Width
        _editBtn.Location = New Point(x, 18)

        _contentH = Math.Max(300, Height - TopY - 24)
        Dim sideW As Integer = Math.Max(340, Math.Min(440, CInt(Width * 0.23)))
        Dim gap As Integer = 24
        Dim mapW As Integer = Math.Max(300, Width - Mx - 52 - gap - sideW)
        _map.SetBounds(Mx, TopY, mapW, _contentH)
        _listCard.SetBounds(Mx, TopY, mapW, _contentH)
        _card.Left = Mx + mapW + gap
        _card.Top = TopY
        _card.Width = Width - 52 - _card.Left
        RelayoutSide()
    End Sub

    Private Sub RelayoutSide()
        Dim w As Integer = Math.Max(200, _card.Width - 16)
        Dim need As Integer = 200
        If _pOverview.Visible Then
            need = LayoutOverview(w)
        ElseIf _pStall.Visible Then
            need = LayoutStall(w)
        ElseIf _pVacant.Visible Then
            need = LayoutVacant(w)
        ElseIf _pEdit.Visible Then
            need = LayoutEdit(w)
        End If
        If _mode = "edit" Then
            _card.Height = _contentH
        Else
            _card.Height = Math.Min(_contentH, need + 16)
        End If
    End Sub

    Private Function LayoutOverview(w As Integer) As Integer
        Dim pad As Integer = 24, y As Integer = 24, iw As Integer = w - pad * 2
        _ovTitle.SetBounds(pad, y, iw, 38) : y += 40
        _ovSub.SetBounds(pad, y, iw, 24) : y += 36
        Dim tw As Integer = (iw - 12) \ 2, th As Integer = 100
        _stPaid.SetBounds(pad, y, tw, th)
        _stUnpaid.SetBounds(pad + tw + 12, y, tw, th)
        y += th + 12
        _stOver.SetBounds(pad, y, tw, th)
        _stVacant.SetBounds(pad + tw + 12, y, tw, th)
        y += th + 16
        If _ovFlash.Text <> "" Then
            StoreStyle.Fit(_ovFlash, iw)
            _ovFlash.Location = New Point(pad, y)
            _ovFlash.Visible = True
            y += _ovFlash.Height + 8
        Else
            _ovFlash.Visible = False
        End If
        StoreStyle.Fit(_ovHint, iw)
        _ovHint.Location = New Point(pad, y)
        y += _ovHint.Height
        Return y + pad
    End Function

    Private Function LayoutStall(w As Integer) As Integer
        Dim pad As Integer = 24, y As Integer = 24, iw As Integer = w - pad * 2
        _spLogo.Location = New Point(pad, y)
        _spName.SetBounds(pad + 76, y + 2, iw - 76, 32)
        _spSub.SetBounds(pad + 76, y + 36, iw - 76, 24)
        y += 74
        _spPillRent.FitWidth()
        _spPillRent.Location = New Point(pad, y)
        _spPillOpen.FitWidth()
        _spPillOpen.Location = New Point(pad + _spPillRent.Width + 8, y)
        y += 42
        _spSellerCap.SetBounds(pad, y, 120, 24)
        _spSellerVal.SetBounds(pad + 120, y, iw - 120, 24)
        y += 38

        Dim bx As Integer = 18, biw As Integer = iw - 36
        _spRentCap.SetBounds(bx, 16, biw, 22)
        _spRentAmt.SetBounds(bx, 38, biw, 38)
        StoreStyle.Fit(_spRentInfo, biw)
        _spRentInfo.Location = New Point(bx, 80)
        Dim ry As Integer = 80 + _spRentInfo.Height + 8
        _spRecord.SetBounds(bx, ry, Math.Min(biw, 190), 40)
        _spRecord.Visible = (_spRecord.Tag Is Nothing)
        Dim boxH As Integer = ry + If(_spRecord.Visible, 40, 0) + 18
        _spRent.SetBounds(pad, y, iw, boxH)
        y += boxH + 18

        _spToggle.SetBounds(pad, y, iw, 44) : y += 54
        _spMove.SetBounds(pad, y, iw, 44) : y += 58
        StoreStyle.Fit(_spNote, iw)
        _spNote.Location = New Point(pad, y)
        y += _spNote.Height
        Return y + pad
    End Function

    Private Function LayoutVacant(w As Integer) As Integer
        Dim pad As Integer = 24, y As Integer = 24, iw As Integer = w - pad * 2
        _vpTitle.SetBounds(pad, y, iw, 38) : y += 42
        _vpPill.Location = New Point(pad, y) : y += 46
        If _vpCombo.Items.Count > 0 Then
            _vpCap.SetBounds(pad, y, iw, 24) : y += 30
            _vpCombo.SetBounds(pad, y, iw, 32) : y += 46
            _vpPlace.SetBounds(pad, y, iw, 44) : y += 58
            _vpCap.Visible = True : _vpCombo.Visible = True : _vpPlace.Visible = True : _vpEmpty.Visible = False
        Else
            StoreStyle.Fit(_vpEmpty, iw)
            _vpEmpty.Location = New Point(pad, y) : y += _vpEmpty.Height + 14
            _vpCap.Visible = False : _vpCombo.Visible = False : _vpPlace.Visible = False : _vpEmpty.Visible = True
        End If
        StoreStyle.Fit(_vpHint, iw)
        _vpHint.Location = New Point(pad, y)
        y += _vpHint.Height
        Return y + pad
    End Function

    Private Sub PlacePair(ByRef y As Integer, pad As Integer, iw As Integer, c1 As Label, n1 As Control, c2 As Label, n2 As Control)
        Dim cw As Integer = (iw - 12) \ 2
        c1.SetBounds(pad, y, cw, 18) : c2.SetBounds(pad + cw + 12, y, cw, 18)
        y += 20
        n1.SetBounds(pad, y, cw, 30) : n2.SetBounds(pad + cw + 12, y, cw, 30)
        y += 40
    End Sub

    Private Function LayoutEdit(w As Integer) As Integer
        Dim pad As Integer = 22, y As Integer = 22, iw As Integer = w - pad * 2 - If(_pEdit.VerticalScroll.Visible, 18, 0)
        _edTitle.SetBounds(pad, y, iw, 36) : y += 40
        StoreStyle.Fit(_edHint, iw)
        _edHint.Location = New Point(pad, y) : y += _edHint.Height + 8
        _edFloorCap.SetBounds(pad, y, iw, 22) : y += 26
        PlacePair(y, pad, iw, _edCapW, _nFloorW, _edCapH, _nFloorH)
        _edSel.SetBounds(pad, y, iw, 24) : y += 30
        PlacePair(y, pad, iw, _edCapX, _nX, _edCapY, _nY)
        PlacePair(y, pad, iw, _edCapSW, _nW, _edCapSH, _nH)
        _edCapFace.SetBounds(pad, y, iw, 18) : y += 20
        _cFace.SetBounds(pad, y, iw, 30) : y += 44
        Dim bw As Integer = (iw - 12) \ 2
        _btnAdd.SetBounds(pad, y, bw, 40)
        _btnRemove.SetBounds(pad + bw + 12, y, bw, 40)
        y += 52
        Dim fbw As Integer = (w - pad * 2 - 12) \ 2   ' Cancel / Save sit in a fixed footer, always visible
        Dim errH As Integer = 0
        If _edErr.Text <> "" Then
            StoreStyle.Fit(_edErr, w - pad * 2)
            _edErr.Location = New Point(pad, 8)
            errH = _edErr.Height + 4
        End If
        _edErr.Visible = (_edErr.Text <> "")
        _edFooter.Height = 76 + errH
        _btnCancel.SetBounds(pad, 16 + errH, fbw, 44)
        _btnSave.SetBounds(pad + fbw + 12, 16 + errH, fbw, 44)
        Return y + pad
    End Function

    Private Sub ShowSide(p As Panel)
        For Each c In {_pOverview, _pStall, _pVacant, _pEdit}
            c.Visible = (c Is p)
        Next
        _edFooter.Visible = (p Is _pEdit)
        RelayoutSide()
    End Sub

    ' =============================================================== data

    Private Sub LoadAll()
        _layout = FloorSvc.GetLayout()
        _map.Layout = _layout
        ReloadStalls()
    End Sub

    Private Function StallSig(list As List(Of FloorStall)) As String
        Dim sb As New System.Text.StringBuilder()
        For Each s In list
            sb.Append(s.StallId).Append(":").Append(s.Slot).Append(":").Append(s.RentStatus).Append(":").Append(s.IsOpen).Append(":").Append(s.Name).Append(":").Append(s.Paid).Append(";")
        Next
        Return sb.ToString()
    End Function

    Private Sub NormalizeSlots(list As List(Of FloorStall))
        Dim used As New HashSet(Of Integer)
        For Each s In list
            If s.Slot <> 0 Then
                If _layout.FindSlot(s.Slot) Is Nothing OrElse Not used.Add(s.Slot) Then s.Slot = 0
            End If
        Next
    End Sub

    Private Sub ReloadStalls()
        ApplyStalls(FloorSvc.GetStalls())
    End Sub

    Private Sub ApplyStalls(fresh As List(Of FloorStall))
        NormalizeSlots(fresh)
        _stalls = fresh
        _sig = StallSig(fresh)
        _map.Stalls = _stalls
        FillList()
        Dim placed = _stalls.Where(Function(q) q.Slot <> 0).Count()
        Dim total = _layout.StallSlots().Count
        _countPill.Text = placed & " / " & total
        _countPill.Invalidate()
        If _mode = "edit" Then Return
        Select Case _view
            Case "stall"
                Dim st = FindStall(_selStallId)
                If st Is Nothing Then ShowOverviewPanel() Else ShowStallPanel(st)
            Case "vacant"
                ShowSlotPanel(_selSlot)
            Case Else
                ShowOverviewPanel()
        End Select
    End Sub

    Private Function FindStall(id As Integer) As FloorStall
        Return _stalls.FirstOrDefault(Function(q) q.StallId = id)
    End Function

    Private Sub PollTick(sender As Object, e As EventArgs) Handles _poll.Tick
        If _mode <> "view" OrElse Not Me.Visible OrElse Not Me.IsHandleCreated Then Return
        Try
            Dim fresh = FloorSvc.GetStalls()
            NormalizeSlots(fresh)
            If StallSig(fresh) <> _sig Then ApplyStalls(fresh)
        Catch
        End Try
    End Sub

    Protected Overrides Sub OnVisibleChanged(e As EventArgs)
        MyBase.OnVisibleChanged(e)
        If Visible Then _poll.Start() Else _poll.Stop()
    End Sub

    Private Sub FillList()
        Dim keep = _selStallId
        _list.BeginUpdate()
        _list.Items.Clear()
        For Each st In _stalls.OrderBy(Function(q) If(q.Slot = 0, 9999, q.Slot)).ThenBy(Function(q) q.Name)
            Dim it As New ListViewItem(If(st.Slot = 0, ChrW(8212), st.Slot.ToString()))
            it.UseItemStyleForSubItems = False
            it.SubItems.Add(st.Name)
            it.SubItems.Add(st.Seller)
            it.SubItems.Add(st.Category)
            Dim rt = StoreStyle.RentText(st)
            If st.RentStatus = "overdue" Then rt &= " (" & st.DaysLate & " d)"
            Dim sub1 = it.SubItems.Add(rt)
            sub1.ForeColor = StoreStyle.RentFore(st.RentStatus)
            it.SubItems.Add(If(st.IsOpen, "Open", "Closed"))
            it.Tag = st.StallId
            _list.Items.Add(it)
            If st.StallId = keep Then it.Selected = True
        Next
        _list.EndUpdate()
    End Sub

    Private Function LogoFor(path As String) As Image
        If String.IsNullOrEmpty(path) Then Return Nothing
        If _logos.ContainsKey(path) Then Return _logos(path)
        Dim img As Image = Nothing
        Try
            img = InboxSvc.LoadImg(path)
        Catch
        End Try
        _logos(path) = img
        Return img
    End Function

    ' =============================================================== side panels

    Private Sub ShowOverviewPanel()
        _view = "overview"
        _selStallId = 0
        _selSlot = 0
        Dim placed = _stalls.Where(Function(q) q.Slot <> 0).ToList()
        Dim total = _layout.StallSlots().Count
        _ovSub.Text = placed.Count & " of " & total & " slots are taken."
        _stPaid.Number = placed.Where(Function(q) q.RentStatus = "paid").Count().ToString()
        _stUnpaid.Number = placed.Where(Function(q) q.RentStatus = "unpaid" OrElse q.RentStatus = "partial").Count().ToString()
        _stOver.Number = placed.Where(Function(q) q.RentStatus = "overdue").Count().ToString()
        _stVacant.Number = Math.Max(0, total - placed.Count).ToString()
        For Each st In {_stPaid, _stUnpaid, _stOver, _stVacant}
            st.Invalidate()
        Next
        Dim waiting = _stalls.Where(Function(q) q.Slot = 0).Count()
        If _flash <> "" Then
            _ovFlash.Text = _flash
        ElseIf waiting > 0 Then
            _ovFlash.Text = waiting & If(waiting = 1, " approved stall has", " approved stalls have") & " no slot yet. Click a vacant slot to place one."
        Else
            _ovFlash.Text = ""
        End If
        ShowSide(_pOverview)
    End Sub

    Private Sub ShowSlotPanel(slotNo As Integer)
        Dim st = _stalls.FirstOrDefault(Function(q) q.Slot = slotNo)
        If st IsNot Nothing Then
            ShowStallPanel(st)
        Else
            ShowVacantPanel(slotNo)
        End If
    End Sub

    Private Sub ShowStallPanel(st As FloorStall)
        _view = "stall"
        _selStallId = st.StallId
        _selSlot = st.Slot
        _spLogo.Picture = LogoFor(st.LogoPath)
        _spLogo.Initial = If(String.IsNullOrEmpty(st.Name), "?", st.Name.Substring(0, 1).ToUpper())
        _spLogo.Invalidate()
        _spName.Text = st.Name
        Dim sub0 As String = If(st.Slot = 0, "Not placed on the map", "Slot " & st.Slot)
        If st.Category <> "" Then sub0 &= " " & ChrW(183) & " " & st.Category
        _spSub.Text = sub0
        _spPillRent.Text = StoreStyle.RentText(st)
        _spPillRent.Fore = StoreStyle.RentFore(st.RentStatus)
        _spPillRent.Back = StoreStyle.RentBack(st.RentStatus)
        _spPillOpen.Text = If(st.IsOpen, "Open", "Closed")
        _spPillOpen.Fore = If(st.IsOpen, Color.FromArgb(31, 138, 84), Color.FromArgb(120, 108, 96))
        _spPillOpen.Back = If(st.IsOpen, Color.FromArgb(226, 246, 236), Color.FromArgb(242, 237, 231))
        _spSellerVal.Text = st.Seller
        _spRentAmt.Text = StoreStyle.Peso(st.Rent)
        If st.Balance <= 0D Then
            _spRentInfo.Text = "Paid in full " & ChrW(183) & " Due " & st.DueDate.ToString("MMM d")
            _spRecord.Tag = "hide"
        Else
            Dim info = "Paid " & StoreStyle.Peso(st.Paid) & " " & ChrW(183) & " Balance " & StoreStyle.Peso(st.Balance) & " " & ChrW(183) & " Due " & st.DueDate.ToString("MMM d")
            If st.RentStatus = "overdue" Then info &= " " & ChrW(183) & " " & st.DaysLate & If(st.DaysLate = 1, " day late", " days late")
            _spRentInfo.Text = info
            _spRecord.Tag = Nothing
        End If
        _spToggle.Text = If(st.IsOpen, "Close stall", "Open stall")
        _spMove.Text = If(st.Slot = 0, "Choose a slot", "Move to another slot")
        ShowSide(_pStall)
    End Sub

    Private Sub ShowVacantPanel(slotNo As Integer)
        _view = "vacant"
        _selSlot = slotNo
        _selStallId = 0
        _vpTitle.Text = "Slot " & slotNo
        _vpCombo.Items.Clear()
        For Each st In _stalls.Where(Function(q) q.Slot = 0).OrderBy(Function(q) q.Name)
            _vpCombo.Items.Add(st)
        Next
        If _vpCombo.Items.Count > 0 Then _vpCombo.SelectedIndex = 0
        ShowSide(_pVacant)
    End Sub

    ' =============================================================== actions (view mode)

    Private Sub OnSegChanged()
        If _mode = "edit" Then Return
        Dim listMode = (_seg.SelectedIndex = 1)
        _map.Visible = Not listMode
        _listCard.Visible = listMode
    End Sub

    Private Sub OnListSelect(sender As Object, e As EventArgs)
        If _mode <> "view" OrElse _list.SelectedItems.Count = 0 Then Return
        Dim st = FindStall(CInt(_list.SelectedItems(0).Tag))
        If st IsNot Nothing Then
            _flash = ""
            ShowStallPanel(st)
        End If
    End Sub

    Private Sub OnMapSlotClicked(slotNo As Integer)
        If _mode = "pick" Then
            If _stalls.Any(Function(q) q.Slot = slotNo) Then
                _map.PickText = "That slot is taken. Choose a vacant (dashed) slot."
                _map.Invalidate()
                Return
            End If
            Dim err As String = ""
            If FloorSvc.AssignStall(_pickStallId, slotNo, err) Then
                Dim id = _pickStallId
                EndPick()
                ReloadStalls()
                Dim st = FindStall(id)
                If st IsNot Nothing Then
                    _map.FocusSlot(st.Slot)
                    ShowStallPanel(st)
                End If
            Else
                _map.PickText = err
                _map.Invalidate()
            End If
            Return
        End If
        _flash = ""
        _map.FocusSlot(slotNo)
        ShowSlotPanel(slotNo)
    End Sub

    Private Sub OnMapBack()
        If _mode = "pick" Then
            Dim id = _pickStallId
            EndPick()
            Dim st = FindStall(id)
            If st IsNot Nothing Then ShowStallPanel(st) Else ShowOverviewPanel()
        Else
            _map.ShowOverview()
            ShowOverviewPanel()
        End If
    End Sub

    Private Sub OnToggleOpen(sender As Object, e As EventArgs)
        Dim st = FindStall(_selStallId)
        If st Is Nothing Then Return
        If st.IsOpen Then
            Dim n = FloorSvc.RunningOrders(st.StallId)
            If n > 0 Then
                Dim msg = n & If(n = 1, " order is", " orders are") & " still running. They stay on the seller's board; only new orders are blocked." & vbCrLf & vbCrLf & "Close " & st.Name & "?"
                If MessageBox.Show(FindForm(), msg, "Close stall", MessageBoxButtons.YesNo, MessageBoxIcon.Question) <> DialogResult.Yes Then Return
            End If
        End If
        Dim err As String = ""
        If Not FloorSvc.SetOpen(st.StallId, Not st.IsOpen, err) Then
            MessageBox.Show(FindForm(), err, "Stores", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        ReloadStalls()
    End Sub

    Private Sub OnMoveClick(sender As Object, e As EventArgs)
        Dim st = FindStall(_selStallId)
        If st Is Nothing Then Return
        BeginPick(st)
    End Sub

    Private Sub OnPlaceClick(sender As Object, e As EventArgs)
        Dim st = TryCast(_vpCombo.SelectedItem, FloorStall)
        If st Is Nothing Then Return
        Dim err As String = ""
        If FloorSvc.AssignStall(st.StallId, _selSlot, err) Then
            ReloadStalls()
            Dim now = FindStall(st.StallId)
            If now IsNot Nothing Then ShowStallPanel(now)
        Else
            MessageBox.Show(FindForm(), err, "Stores", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End If
    End Sub

    Private Sub BeginPick(st As FloorStall)
        _mode = "pick"
        _pickStallId = st.StallId
        If _seg.SelectedIndex <> 0 Then _seg.SelectedIndex = 0
        _map.PickText = "Choose a vacant slot for " & st.Name & "."
        _map.PickMode = True
        _map.ShowOverview()
        _flash = "Click a vacant slot on the map to put " & st.Name & " there."
        ShowOverviewPanel()
    End Sub

    Private Sub EndPick()
        _mode = "view"
        _map.PickMode = False
        _map.ShowOverview()
        _flash = ""
    End Sub

    ' =============================================================== edit layout

    Private Sub BeginEdit()
        If _mode <> "view" Then Return
        _poll.Stop()
        If _seg.SelectedIndex <> 0 Then _seg.SelectedIndex = 0
        _mode = "edit"
        _draft = _layout.Clone()
        _origSig = _draft.Signature()
        _editBtn.Visible = False
        _seg.Visible = False
        _title.Text = "Edit layout"
        _map.EnterEdit(_draft)
        _edErr.Text = ""
        SyncEditFields()
        ShowSide(_pEdit)
    End Sub

    Private Sub ExitEdit()
        _mode = "view"
        _editBtn.Visible = True
        _seg.Visible = True
        _title.Text = "Stores"
        _draft = Nothing
        _map.ExitEdit(_layout)
        _flash = ""
        Try
            ReloadStalls()
        Catch
        End Try
        ShowOverviewPanel()
        _poll.Start()
    End Sub

    Private Sub SyncEditFields()
        If _draft Is Nothing Then Return
        _syncing = True
        Try
            Dim minW As Integer = 200, minH As Integer = 200
            For Each q In _draft.Slots
                minW = Math.Max(minW, q.X + q.W)
                minH = Math.Max(minH, q.Y + q.H)
            Next
            _nFloorW.Minimum = minW
            _nFloorH.Minimum = minH
            _nFloorW.Value = Math.Max(_nFloorW.Minimum, Math.Min(_nFloorW.Maximum, _draft.Width))
            _nFloorH.Value = Math.Max(_nFloorH.Minimum, Math.Min(_nFloorH.Maximum, _draft.Height))
            Dim s = _map.EditSelected
            Dim has = (s IsNot Nothing)
            For Each c In {_nX, _nY, _nW, _nH}
                c.Enabled = has
            Next
            _cFace.Enabled = has AndAlso Not s.IsDining
            _btnRemove.Enabled = has AndAlso Not s.IsDining
            If has Then
                Dim stall = _stalls.FirstOrDefault(Function(q) q.Slot = s.Slot AndAlso s.Slot <> 0)
                _edSel.Text = If(s.IsDining, "Dining area", "Slot " & s.Slot & If(stall IsNot Nothing, " " & ChrW(183) & " " & stall.Name, " " & ChrW(183) & " vacant"))
                _nX.Maximum = Math.Max(0, _draft.Width - s.W)
                _nY.Maximum = Math.Max(0, _draft.Height - s.H)
                _nW.Maximum = Math.Max(_nW.Minimum, _draft.Width - s.X)
                _nH.Maximum = Math.Max(_nH.Minimum, _draft.Height - s.Y)
                _nX.Value = Clamp(s.X, _nX)
                _nY.Value = Clamp(s.Y, _nY)
                _nW.Value = Clamp(s.W, _nW)
                _nH.Value = Clamp(s.H, _nH)
                Dim idx = Array.IndexOf(_faces, s.Face)
                _cFace.SelectedIndex = If(idx < 0, 2, idx)
            Else
                _edSel.Text = "Click a slot on the map to edit it"
                _cFace.SelectedIndex = -1
            End If
        Finally
            _syncing = False
        End Try
    End Sub

    Private Function Clamp(v As Integer, n As NumericUpDown) As Decimal
        Return Math.Max(n.Minimum, Math.Min(n.Maximum, v))
    End Function

    Private Sub OnEditNumeric(sender As Object, e As EventArgs)
        If _syncing OrElse _draft Is Nothing Then Return
        _edErr.Text = ""
        If sender Is _nFloorW OrElse sender Is _nFloorH Then
            _draft.Width = CInt(_nFloorW.Value)
            _draft.Height = CInt(_nFloorH.Value)
            _map.RefitCamera()
            Return
        End If
        Dim s = _map.EditSelected
        If s Is Nothing Then Return
        If sender IsNot _cFace Then
            Dim nx As Integer = CInt(_nX.Value), ny As Integer = CInt(_nY.Value), nw As Integer = CInt(_nW.Value), nh As Integer = CInt(_nH.Value)
            If nx + nw > _draft.Width OrElse ny + nh > _draft.Height Then
                _edErr.Text = "That would stick out of the floor (" & _draft.Width & " x " & _draft.Height & ")."
                SyncEditFields() : RelayoutSide() : Return
            End If
            If Not s.IsDining Then
                Dim probe As New FloorSlot With {.X = nx, .Y = ny, .W = nw, .H = nh}
                For Each q In _draft.Slots
                    If q IsNot s AndAlso Not q.IsDining AndAlso FloorSvc.Overlap(probe, q) Then
                        _edErr.Text = "That would overlap slot " & q.Slot & "."
                        SyncEditFields() : RelayoutSide() : Return
                    End If
                Next
            End If
        End If
        s.X = CInt(_nX.Value) : s.Y = CInt(_nY.Value) : s.W = CInt(_nW.Value) : s.H = CInt(_nH.Value)
        If Not s.IsDining AndAlso _cFace.SelectedIndex >= 0 Then s.Face = _faces(_cFace.SelectedIndex)
        _map.Invalidate()
    End Sub

    Private Function FindFreeSpot(w As Integer, h As Integer) As Point
        For pass = 0 To 1
            For y = 0 To _draft.Height - h Step 10
                For x = 0 To _draft.Width - w Step 10
                    Dim probe As New FloorSlot With {.X = x, .Y = y, .W = w, .H = h}
                    Dim clash As Boolean = False
                    For Each q In _draft.Slots
                        If q.IsDining AndAlso pass = 1 Then Continue For
                        If FloorSvc.Overlap(probe, q) Then
                            clash = True
                            Exit For
                        End If
                    Next
                    If Not clash Then Return New Point(x, y)
                Next
            Next
        Next
        Return New Point(0, 0)
    End Function

    Private Sub AddSlot()
        If _draft Is Nothing Then Return
        Dim w As Integer = 200, h As Integer = 140
        Dim p = FindFreeSpot(w, h)
        Dim s As New FloorSlot With {.Id = 0, .Slot = _draft.NextSlot, .Kind = "stall", .X = p.X, .Y = p.Y, .W = w, .H = h, .Face = "s"}
        _draft.NextSlot += 1
        _draft.Slots.Add(s)
        _map.EditSelected = s
        _edErr.Text = ""
        SyncEditFields()
        RelayoutSide()
    End Sub

    Private Sub RemoveSlot()
        Dim s = _map.EditSelected
        If s Is Nothing OrElse s.IsDining Then Return
        Dim stall = _stalls.FirstOrDefault(Function(q) q.Slot = s.Slot)
        If stall IsNot Nothing Then
            _edErr.Text = "Slot " & s.Slot & " still has " & stall.Name & ". Move that stall to another slot first."
            RelayoutSide()
            Return
        End If
        _draft.Slots.Remove(s)
        _map.EditSelected = Nothing
        _edErr.Text = ""
        SyncEditFields()
        RelayoutSide()
    End Sub

    Private Sub SaveEdit()
        If _draft Is Nothing Then Return
        _edErr.Text = ""
        For Each box In {_nFloorW, _nFloorH, _nX, _nY, _nW, _nH}
            Dim committed = box.Value   ' reading Value commits text typed but not yet confirmed
        Next
        If _edErr.Text <> "" Then
            RelayoutSide()
            Return
        End If
        Dim err As String = ""
        If FloorSvc.SaveLayout(_draft, err) Then
            Try
                _layout = FloorSvc.GetLayout()
            Catch
            End Try
            ExitEdit()
            _flash = "Layout saved."
            ShowOverviewPanel()
        Else
            _edErr.Text = err
            RelayoutSide()
        End If
    End Sub

    Private Sub CancelEdit()
        If _draft Is Nothing Then Return
        If _draft.Signature() <> _origSig Then
            If MessageBox.Show(FindForm(), "Discard your layout changes?", "Edit layout", MessageBoxButtons.YesNo, MessageBoxIcon.Question) <> DialogResult.Yes Then Return
        End If
        ExitEdit()
    End Sub
End Class
