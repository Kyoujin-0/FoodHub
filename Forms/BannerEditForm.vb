Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.IO
Imports System.Windows.Forms

''' <summary>
''' Shows the banner like the customer Home does. Drag inside it to move the photo, change .Zoom to enlarge it.
''' Headline and fade are drawn on top so the admin sees the real result.
''' </summary>
Public Class BannerPositionBox
    Inherits Control

    Public Event PositionChanged As EventHandler

    Public Property Photo As Image
    Public Property FocusX As Double = 0.5
    Public Property FocusY As Double = 0.5
    Public Property Zoom As Double = 1.0
    Public Property Headline As String = ""
    Public Property ShowButton As Boolean
    Public Property ShowSafe As Boolean

    Private _drag As Boolean
    Private _downX As Integer
    Private _downY As Integer
    Private _fx0 As Double
    Private _fy0 As Double

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        BackColor = Color.White
    End Sub

    Private Function Area() As Rectangle
        Return New Rectangle(0, 0, Width - 1, Height - 1)
    End Function

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        BannerPaint.Draw(g, Area(), Photo, FocusX, FocusY, Zoom, Headline, "new banner", ShowButton, ShowSafe, 18)
        If Photo Is Nothing Then
            TextRenderer.DrawText(g, "Choose a photo to place it here", Theme.F(10.5F, True), New Rectangle(Width \ 2, 0, Width \ 2 - 20, Height), Color.FromArgb(235, 255, 255, 255),
                                  TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
        End If
    End Sub

    Protected Overrides Sub OnMouseEnter(e As EventArgs)
        MyBase.OnMouseEnter(e)
        Cursor = If(Photo IsNot Nothing, Cursors.SizeAll, Cursors.Default)
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If e.Button <> MouseButtons.Left OrElse Photo Is Nothing Then Return
        _drag = True
        _downX = e.X
        _downY = e.Y
        _fx0 = FocusX
        _fy0 = FocusY
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        If Not _drag OrElse Photo Is Nothing Then Return
        Dim r As Rectangle = Area()
        Dim ir As Rectangle = BannerPaint.ImageRect(r, Photo, _fx0, _fy0, Zoom)
        Dim rangeX As Integer = r.Width - ir.Width   ' zero or negative: how far the photo can slide sideways
        Dim rangeY As Integer = r.Height - ir.Height
        If rangeX < -1 Then FocusX = Clamp01((_fx0 * rangeX + (e.X - _downX)) / rangeX)
        If rangeY < -1 Then FocusY = Clamp01((_fy0 * rangeY + (e.Y - _downY)) / rangeY)
        Invalidate()
        RaiseEvent PositionChanged(Me, EventArgs.Empty)
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        _drag = False
    End Sub

    Private Function Clamp01(v As Double) As Double
        Return Math.Max(0.0, Math.Min(1.0, v))
    End Function
End Class

''' <summary>Add / edit one banner: photo (with position and zoom), admin title, headline, stall it opens, active.</summary>
Public Class BannerEditForm
    Inherits Form

    ''' <summary>The values to save (ImagePath is filled in by the page).</summary>
    Public Result As BannerRow
    ''' <summary>A picture file picked in this dialog that still has to be stored. Empty = keep the current photo.</summary>
    Public NewImageFile As String = ""
    Public PhotoRemoved As Boolean
    Public Deleted As Boolean

    Private ReadOnly _row As BannerRow
    Private ReadOnly _box As New BannerPositionBox()
    Private ReadOnly _zoom As New TrackBar()
    Private ReadOnly _title As New AppTextBox()
    Private ReadOnly _headline As New AppTextBox()
    Private ReadOnly _count As Label
    Private ReadOnly _stall As New ComboBox()
    Private ReadOnly _active As New CheckBox()
    Private ReadOnly _remove As New BannerActionButton()
    Private _owned As Image

    Public Sub New(row As BannerRow, stalls As List(Of StallChoice))
        _row = row
        Dim isNew As Boolean = (row.Id = 0)
        Text = If(isNew, "Add banner", "Edit banner")
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        ShowInTaskbar = False
        StartPosition = FormStartPosition.CenterParent
        ClientSize = New Size(720, 720)
        BackColor = Color.White
        Font = Theme.F(10)
        KeyPreview = True

        Dim head As Label = Ui.Lbl(If(isNew, "Add banner", "Edit banner"), 17, True)
        head.BackColor = Color.White
        head.Location = New Point(28, 20)
        Controls.Add(head)

        ' photo position box
        _box.SetBounds(28, 66, 664, CInt(664 / BannerPaint.AspectRatio))
        Controls.Add(_box)
        Dim hint As Label = Ui.Lbl("Drag the photo to move it. Use the slider to zoom in.", 10, False, Theme.TextMuted)
        hint.BackColor = Color.White
        hint.Location = New Point(28, _box.Bottom + 8)
        Controls.Add(hint)
        Dim reset As New LinkLabel()
        reset.Text = "Reset position"
        reset.AutoSize = True
        reset.Font = Theme.F(10, True)
        reset.LinkColor = Theme.Primary
        reset.ActiveLinkColor = Theme.PrimaryDark
        reset.BackColor = Color.White
        reset.Location = New Point(692 - 100, _box.Bottom + 8)
        AddHandler reset.LinkClicked, Sub(s, e) ResetPosition()
        Controls.Add(reset)

        ' photo buttons + zoom
        Dim y As Integer = _box.Bottom + 44
        Controls.Add(Caption("Photo", 28, y))
        Dim choose As New BannerActionButton()
        choose.Text = "Choose photo"
        choose.Kind = 1
        choose.SetBounds(28, y + 26, 140, 42)
        AddHandler choose.Click, Sub(s, e) ChoosePhoto()
        Controls.Add(choose)
        _remove.Text = "Remove"
        _remove.Kind = 1
        _remove.SetBounds(178, y + 26, 110, 42)
        AddHandler _remove.Click, Sub(s, e) RemovePhoto()
        Controls.Add(_remove)

        Controls.Add(Caption("Zoom", 330, y))
        _zoom.Minimum = 100
        _zoom.Maximum = 300
        _zoom.TickStyle = TickStyle.None
        _zoom.SmallChange = 5
        _zoom.LargeChange = 25
        _zoom.Value = Math.Max(100, Math.Min(300, CInt(row.Zoom * 100)))
        _zoom.SetBounds(324, y + 24, 372, 45)
        _zoom.BackColor = Color.White
        AddHandler _zoom.ValueChanged, Sub(s, e) OnZoom()
        Controls.Add(_zoom)

        ' title (admin only)
        y += 92
        Controls.Add(Caption("Title (only you see this)", 28, y))
        _title.Font = Theme.F(11)
        _title.MaxLength = BannerSvc.MaxTitle
        _title.PlaceholderText = "e.g. Isles Banner"
        _title.BackColor = Color.White
        _title.SetBounds(28, y + 26, 664, 50)
        _title.Inner.Text = row.Title
        Controls.Add(_title)

        ' headline
        y += 92
        Controls.Add(Caption("Headline (shown on the banner)", 28, y))
        _count = Ui.Lbl("", 10, False, Theme.TextMuted)
        _count.BackColor = Color.White
        _count.AutoSize = False
        _count.TextAlign = ContentAlignment.MiddleRight
        _count.SetBounds(492, y, 200, 22)
        Controls.Add(_count)
        _headline.Font = Theme.F(11)
        _headline.MaxLength = BannerSvc.MaxHeadline
        _headline.PlaceholderText = "e.g. Good Food, Good Vibes"
        _headline.BackColor = Color.White
        _headline.SetBounds(28, y + 26, 664, 50)
        _headline.Inner.Text = row.Headline
        Controls.Add(_headline)
        AddHandler _headline.Inner.TextChanged, Sub(s, e) OnHeadline()

        ' stall link
        y += 92
        Controls.Add(Caption("When someone taps the banner, open", 28, y))
        _stall.DropDownStyle = ComboBoxStyle.DropDownList
        _stall.Font = Theme.F(11)
        _stall.FlatStyle = FlatStyle.Flat
        _stall.BackColor = Color.White
        _stall.SetBounds(28, y + 26, 664, 34)
        Dim none As New StallChoice()
        none.Id = 0
        none.Name = "No link (just a picture)"
        _stall.Items.Add(none)
        _stall.SelectedIndex = 0
        For Each s As StallChoice In stalls
            Dim idx As Integer = _stall.Items.Add(s)
            If s.Id = row.StallId Then _stall.SelectedIndex = idx
        Next
        AddHandler _stall.SelectedIndexChanged, Sub(s, e) OnStallChanged()
        Controls.Add(_stall)

        ' active
        _active.Text = "Show this banner on the customer home"
        _active.Checked = row.IsActive
        _active.AutoSize = True
        _active.Font = Theme.F(10.5F, True)
        _active.ForeColor = Theme.TextDark
        _active.BackColor = Color.White
        _active.Cursor = Cursors.Hand
        _active.Location = New Point(28, y + 74)
        Controls.Add(_active)

        ' buttons
        Dim by As Integer = ClientSize.Height - 66
        If Not isNew Then
            Dim del As New BannerActionButton()
            del.Text = "Delete"
            del.Kind = 2
            del.SetBounds(28, by, 110, 44)
            AddHandler del.Click, Sub(s, e) DoDelete()
            Controls.Add(del)
        End If
        Dim cancel As New BannerActionButton()
        cancel.Text = "Cancel"
        cancel.Kind = 1
        cancel.SetBounds(692 - 110 - 12 - 130, by, 110, 44)
        AddHandler cancel.Click, Sub(s, e) DialogResult = DialogResult.Cancel
        Controls.Add(cancel)
        Dim save As New BannerActionButton()
        save.Text = If(isNew, "Add banner", "Save")
        save.Kind = 0
        save.SetBounds(692 - 130, by, 130, 44)
        AddHandler save.Click, Sub(s, e) DoSave()
        Controls.Add(save)

        ' start values
        _box.FocusX = row.FocusX
        _box.FocusY = row.FocusY
        _box.Zoom = Math.Max(1.0, row.Zoom)
        _box.Headline = row.Headline
        _box.ShowButton = row.StallId > 0
        _box.Photo = BannerSvc.LoadImage(row.ImagePath)
        UpdateCount()
        UpdateRemove()
    End Sub

    Private Function Caption(text As String, x As Integer, y As Integer) As Label
        Dim l As Label = Ui.Lbl(text, 10.5F, True)
        l.BackColor = Color.White
        l.Location = New Point(x, y)
        Return l
    End Function

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing AndAlso _owned IsNot Nothing Then
            _box.Photo = Nothing
            _owned.Dispose()
            _owned = Nothing
        End If
        MyBase.Dispose(disposing)
    End Sub

    Protected Overrides Function ProcessCmdKey(ByRef msg As Message, keyData As Keys) As Boolean
        If keyData = Keys.Escape Then
            DialogResult = DialogResult.Cancel
            Return True
        End If
        Return MyBase.ProcessCmdKey(msg, keyData)
    End Function

    ' ---------------- small handlers ----------------
    Private Sub UpdateCount()
        _count.Text = _headline.Inner.Text.Length.ToString() & " / " & BannerSvc.MaxHeadline.ToString()
    End Sub

    Private Sub UpdateRemove()
        _remove.Visible = (_box.Photo IsNot Nothing)
    End Sub

    Private Sub OnHeadline()
        _box.Headline = _headline.Inner.Text.Trim()
        _box.Invalidate()
        UpdateCount()
    End Sub

    Private Sub OnZoom()
        _box.Zoom = _zoom.Value / 100.0
        _box.Invalidate()
    End Sub

    Private Sub OnStallChanged()
        Dim s As StallChoice = TryCast(_stall.SelectedItem, StallChoice)
        _box.ShowButton = (s IsNot Nothing AndAlso s.Id > 0)
        _box.Invalidate()
    End Sub

    Private Sub ResetPosition()
        _box.FocusX = 0.5
        _box.FocusY = 0.5
        _zoom.Value = 100
        _box.Zoom = 1.0
        _box.Invalidate()
    End Sub

    Private Sub ChoosePhoto()
        Using dlg As New OpenFileDialog()
            dlg.Title = "Choose a banner photo"
            dlg.Filter = "Pictures|*.jpg;*.jpeg;*.png;*.bmp;*.gif|All files|*.*"
            If dlg.ShowDialog(Me) <> DialogResult.OK Then Return
            Dim img As Image
            Try
                img = BannerSvc.LoadFile(dlg.FileName, 1800)
            Catch
                MessageBox.Show("That file could not be opened as a picture.", "Banner photo", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End Try
            Dim previous As Image = _owned
            _box.Photo = img
            _owned = img
            If previous IsNot Nothing Then previous.Dispose()
            NewImageFile = dlg.FileName
            PhotoRemoved = False
            ResetPosition()
            UpdateRemove()
        End Using
    End Sub

    Private Sub RemovePhoto()
        _box.Photo = Nothing
        If _owned IsNot Nothing Then
            _owned.Dispose()
            _owned = Nothing
        End If
        NewImageFile = ""
        PhotoRemoved = True
        _box.Invalidate()
        UpdateRemove()
    End Sub

    Private Sub DoDelete()
        If Not Ui.Confirm("Delete this banner? This cannot be undone.") Then Return
        Deleted = True
        DialogResult = DialogResult.OK
    End Sub

    Private Sub DoSave()
        Dim r As New BannerRow()
        r.Id = _row.Id
        r.Title = _title.Inner.Text.Trim()
        r.Headline = _headline.Inner.Text.Trim()
        Dim s As StallChoice = TryCast(_stall.SelectedItem, StallChoice)
        r.StallId = If(s IsNot Nothing, s.Id, 0)
        r.IsActive = _active.Checked
        r.FocusX = _box.FocusX
        r.FocusY = _box.FocusY
        r.Zoom = _box.Zoom
        r.ImagePath = _row.ImagePath
        Result = r
        DialogResult = DialogResult.OK
    End Sub
End Class
