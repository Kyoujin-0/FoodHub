Imports System.Data
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports Timer = System.Windows.Forms.Timer

''' <summary>Seller "Store" tab: photos and details of the stall on the left, live preview of what customers see on the right,
''' and a button for the Sales records popup.</summary>
Public Class SellerStorePage
    Inherits PageBase

    ''' <summary>Raised after a successful save so the top bar can show the new name and logo.</summary>
    Public Event StoreSaved(stallName As String, logoPath As String)

    Private ReadOnly _stallId As Integer
    Private ReadOnly _accent As New Panel()
    Private ReadOnly _title As Label
    Private ReadOnly _subtitle As Label
    Private ReadOnly _error As Label
    Private ReadOnly _salesBtn As RoundedButton

    ' photos card
    Private ReadOnly _photos As New RoundedPanel()
    Private ReadOnly _photosTitle As Label
    Private ReadOnly _logoBox As New PhotoBox()
    Private ReadOnly _coverBox As New PhotoBox()
    Private ReadOnly _logoTitle As Label
    Private ReadOnly _logoHint As Label
    Private ReadOnly _coverTitle As Label
    Private ReadOnly _coverHint As Label
    Private ReadOnly _logoChange As RoundedButton
    Private ReadOnly _logoRemove As RoundedButton
    Private ReadOnly _coverChange As RoundedButton
    Private ReadOnly _coverRemove As RoundedButton

    ' details card
    Private ReadOnly _details As New RoundedPanel()
    Private ReadOnly _detailsTitle As Label
    Private ReadOnly _lblName As Label
    Private ReadOnly _lblCat As Label
    Private ReadOnly _catHint As Label
    Private ReadOnly _lblDesc As Label
    Private ReadOnly _lblContact As Label
    Private ReadOnly _contactErr As Label
    Private ReadOnly _name As New AppTextBox()
    Private ReadOnly _cat As New StoreCombo()
    Private ReadOnly _desc As New AreaField()
    Private ReadOnly _contact As New AppTextBox()
    Private ReadOnly _save As RoundedButton
    Private ReadOnly _discard As RoundedButton
    Private ReadOnly _info As Label

    ' preview card
    Private ReadOnly _preview As New RoundedPanel()
    Private ReadOnly _prevTitle As Label
    Private ReadOnly _prevSub As Label
    Private ReadOnly _cap1 As Label
    Private ReadOnly _cap2 As Label
    Private ReadOnly _banner As New StallBannerPreview()
    Private _homeCard As StallCard
    Private _prevInnerW As Integer = 380

    Private ReadOnly _poll As New Timer()
    Private ReadOnly _staged As New List(Of String)
    Private _logoImg As Image
    Private _coverImg As Image
    Private _logo As String = ""
    Private _cover As String = ""
    Private _oName As String = ""
    Private _oCat As Integer
    Private _oContact As String = ""
    Private _oDesc As String = ""
    Private _oLogo As String = ""
    Private _oCover As String = ""
    Private _isOpen As Boolean
    Private _loading As Boolean
    Private _failed As Boolean

    Public Sub New(stallId As Integer)
        _stallId = stallId

        _accent.BackColor = Theme.Primary
        _title = Ui.Lbl("Store", 28, True)
        _subtitle = Ui.Lbl("What customers see on Home and on your stall page", 11, False, Theme.TextMuted)
        _error = Ui.WrapLbl("", 11, 600, Theme.Danger)
        _salesBtn = Ui.OutlineBtn("Sales records", 190, 46)
        _salesBtn.IconName = "receipt"

        For Each card As RoundedPanel In New RoundedPanel() {_photos, _details, _preview}
            card.Radius = 24
            card.FillColor = Color.White
            card.SurroundColor = Theme.Bg
            card.Shadow = True
        Next

        ' photos
        _photosTitle = Ui.Lbl("Photos", 15, True)
        _logoBox.Circle = True
        _logoTitle = Ui.Lbl("Logo", 13, True)
        _logoHint = Ui.WrapLbl("Square works best. It is also your profile picture.", 10, 300, Theme.TextMuted)
        _coverTitle = Ui.Lbl("Cover photo", 13, True)
        _coverHint = Ui.WrapLbl("Wide photo of your stall or food.", 10, 300, Theme.TextMuted)
        _logoChange = Ui.OutlineBtn("Change", 110, 42)
        _logoRemove = Ui.SoftBtn("Remove", 100, 42)
        _coverChange = Ui.OutlineBtn("Change", 110, 42)
        _coverRemove = Ui.SoftBtn("Remove", 100, 42)
        For Each c As Control In New Control() {_photosTitle, _logoBox, _coverBox, _logoTitle, _logoHint, _coverTitle, _coverHint, _logoChange, _logoRemove, _coverChange, _coverRemove}
            _photos.Controls.Add(c)
        Next

        ' details
        _detailsTitle = Ui.Lbl("Details", 15, True)
        _lblName = Ui.Lbl("Stall name *", 10.5F, False, Theme.TextMuted)
        _lblCat = Ui.Lbl("Category", 10.5F, False, Theme.TextMuted)
        _catHint = Ui.WrapLbl("Shown under your name. Customers can filter Home by category.", 10, 400, Theme.TextMuted)
        _lblDesc = Ui.Lbl("Description", 10.5F, False, Theme.TextMuted)
        _lblContact = Ui.Lbl("Contact number *", 10.5F, False, Theme.TextMuted)
        _contactErr = Ui.Lbl("", 9.5F, False, Theme.Danger)
        _name.Font = Theme.F(11)
        _name.MaxLength = 60
        _contact.Font = Theme.F(11)
        _contact.MaxLength = 13
        _contact.PlaceholderText = "09XXXXXXXXX or +639XXXXXXXXX"
        _desc.MaxLen = 200
        _save = Ui.PrimaryBtn("Save changes", 180, 48)
        _discard = Ui.SoftBtn("Discard", 120, 48)
        _info = Ui.Lbl("", 10.5F, False, Theme.Primary)
        For Each c As Control In New Control() {_detailsTitle, _lblName, _name, _lblCat, _cat, _catHint, _lblDesc, _desc, _lblContact, _contact, _contactErr, _save, _discard, _info}
            _details.Controls.Add(c)
        Next

        ' preview
        _prevTitle = Ui.Lbl("Live preview", 15, True)
        _prevSub = Ui.WrapLbl("This is how customers see your stall.", 10.5F, 360, Theme.TextMuted)
        _cap1 = Ui.Lbl("ON HOME", 8.5F, True, Theme.TextMuted)
        _cap2 = Ui.Lbl("ON YOUR STALL PAGE", 8.5F, True, Theme.TextMuted)
        For Each c As Control In New Control() {_prevTitle, _prevSub, _cap1, _banner, _cap2}
            _preview.Controls.Add(c)
        Next

        Controls.Add(_accent)
        Controls.Add(_title)
        Controls.Add(_subtitle)
        Controls.Add(_salesBtn)
        Controls.Add(_error)
        Controls.Add(_photos)
        Controls.Add(_details)
        Controls.Add(_preview)

        AddHandler _salesBtn.Click, AddressOf OnSales
        AddHandler _logoChange.Click, Sub(s, e) PickPhoto(True)
        AddHandler _coverChange.Click, Sub(s, e) PickPhoto(False)
        AddHandler _logoRemove.Click, Sub(s, e) SetPhoto(True, "")
        AddHandler _coverRemove.Click, Sub(s, e) SetPhoto(False, "")
        AddHandler _name.TextChanged, AddressOf OnNameChanged
        AddHandler _cat.SelectionChanged, Sub(s, e) UpdatePreview()
        AddHandler _save.Click, AddressOf OnSave
        AddHandler _discard.Click, AddressOf OnDiscard

        LoadStore()
        _poll.Interval = 300
        AddHandler _poll.Tick, Sub(s, e) RefreshState()
        _poll.Start()
    End Sub

    ' ---------------- data ----------------

    Private Sub LoadStore()
        _loading = True
        Try
            Dim st As DataRow = StoreSvc.GetStore(_stallId)
            If st Is Nothing Then Throw New InvalidOperationException("Stall not found.")
            Dim cats As DataTable = CatalogSvc.GetStallCategories()
            Dim names As New List(Of String)
            Dim ids As New List(Of Integer)
            For Each r As DataRow In cats.Rows
                names.Add(Db.GetStr(r, "name"))
                ids.Add(Db.GetInt(r, "id"))
            Next
            _cat.SetItems(names, ids)
            _oName = Db.GetStr(st, "name")
            _oCat = Db.GetInt(st, "category_id")
            _oContact = Db.GetStr(st, "contact")
            _oDesc = Db.GetStr(st, "description")
            _oLogo = Db.GetStr(st, "logo_path")
            _oCover = Db.GetStr(st, "cover_path")
            _isOpen = Db.GetBool(st, "is_open")
            _name.Text = _oName
            _cat.SelectedId = _oCat
            _contact.Text = _oContact
            _desc.Value = _oDesc
            _logo = _oLogo
            _cover = _oCover
            _logoImg = InboxSvc.LoadImg(_logo)
            _coverImg = InboxSvc.LoadImg(_cover)
            _logoBox.Seed = _oName
            _coverBox.Seed = _oName & "cover"
            _logoBox.SetImage(_logoImg)
            _coverBox.SetImage(_coverImg)
            _failed = False
        Catch ex As Exception
            _failed = True
            _error.Text = "The Store page could not be loaded (" & ex.Message & "). Make sure patch_store.sql was run on your database."
        End Try
        _loading = False
        _photos.Visible = Not _failed
        _details.Visible = Not _failed
        _preview.Visible = Not _failed
        _salesBtn.Visible = Not _failed
        _error.Visible = _failed
        If Not _failed Then
            UpdatePreview()
            RefreshState()
        End If
    End Sub

    Private Function IsDirty() As Boolean
        Return _name.Text.Trim() <> _oName OrElse _cat.SelectedId <> _oCat OrElse _contact.Text.Trim() <> _oContact OrElse
               _desc.Value.Trim() <> _oDesc OrElse _logo <> _oLogo OrElse _cover <> _oCover
    End Function

    Private Sub RefreshState()
        If _failed OrElse _loading Then Return
        Dim ct As String = _contact.Text.Trim()
        Dim bad As Boolean = (ct <> "" AndAlso Not Validator.PhoneOk(ct))
        Dim msg As String = ""
        If bad Then
            If Not Validator.PhoneStartOk(ct) Then
                msg = "Number must start with 09 or +63."
            Else
                msg = "Use 09XXXXXXXXX or +639XXXXXXXXX."
            End If
        End If
        If _contactErr.Text <> msg Then _contactErr.Text = msg
        Dim dirty As Boolean = IsDirty()
        If _save.Enabled <> dirty Then _save.Enabled = dirty
        If _discard.Enabled <> dirty Then _discard.Enabled = dirty
        Dim t As String = If(dirty, "You have unsaved changes", "")
        If _info.Text <> t Then _info.Text = t
    End Sub

    ' ---------------- preview ----------------

    Private Sub UpdatePreview()
        If _loading OrElse _failed Then Return
        If _homeCard IsNot Nothing Then
            _preview.Controls.Remove(_homeCard)
            _homeCard.Dispose()
        End If
        Dim nm As String = If(_name.Text.Trim() = "", "Your stall", _name.Text.Trim())
        Dim cat As String = If(_cat.SelectedName = "", "Food", _cat.SelectedName)
        _homeCard = New StallCard(_stallId, nm, cat, _isOpen, _coverImg, _logoImg)
        _homeCard.BackColor = Color.White
        _homeCard.Cursor = Cursors.Default
        _preview.Controls.Add(_homeCard)
        _homeCard.SetBounds(24, 120, _prevInnerW, 214)
        _banner.Setup(nm, cat, _logoImg, _isOpen)
    End Sub

    Private Sub OnNameChanged(sender As Object, e As EventArgs)
        If _loading Then Return
        Dim nm As String = _name.Text.Trim()
        _logoBox.SetSeed(nm)
        _coverBox.SetSeed(nm & "cover")
        UpdatePreview()
    End Sub

    ' ---------------- photos ----------------

    Private Sub PickPhoto(isLogo As Boolean)
        Dim f As String = MenuFmt.PickImage(FindForm())
        If f = "" Then Return
        Dim err As String = ""
        Dim rel As String = StoreSvc.SaveStallImage(f, isLogo, err)
        If rel = "" Then
            Ui.Warn(err)
            Return
        End If
        _staged.Add(rel)
        SetPhoto(isLogo, rel)
    End Sub

    Private Sub SetPhoto(isLogo As Boolean, rel As String)
        Dim img As Image = Nothing
        If rel <> "" Then img = InboxSvc.LoadImg(rel)
        Dim old As Image
        If isLogo Then
            _logo = rel
            old = _logoImg
            _logoImg = img
            _logoBox.SetImage(img)
        Else
            _cover = rel
            old = _coverImg
            _coverImg = img
            _coverBox.SetImage(img)
        End If
        UpdatePreview()
        RefreshState()
        If old IsNot Nothing Then old.Dispose()
    End Sub

    ' ---------------- save / discard ----------------

    Private Sub OnSave(sender As Object, e As EventArgs)
        Dim nm As String = _name.Text.Trim()
        If nm = "" Then
            Ui.Warn("Please enter your stall name.")
            Return
        End If
        Dim ct As String = _contact.Text.Trim()
        If Not Validator.PhoneOk(ct) Then
            Ui.Warn("Please enter a valid Philippine mobile number (09XXXXXXXXX or +639XXXXXXXXX).")
            Return
        End If
        Dim err As String = ""
        If Not StoreSvc.SaveStore(_stallId, nm, _cat.SelectedId, ct, _desc.Value.Trim(), _logo, _cover, err) Then
            Ui.Warn(err)
            Return
        End If
        If _oLogo <> _logo Then StoreSvc.DeleteStallImage(_oLogo)
        If _oCover <> _cover Then StoreSvc.DeleteStallImage(_oCover)
        _oName = nm
        _oCat = _cat.SelectedId
        _oContact = ct
        _oDesc = _desc.Value.Trim()
        _oLogo = _logo
        _oCover = _cover
        _staged.Clear()
        RefreshState()
        Toast.Show(FindForm(), "Store updated")
        RaiseEvent StoreSaved(nm, _logo)
    End Sub

    Private Sub OnDiscard(sender As Object, e As EventArgs)
        If Not Ui.Confirm("Discard your unsaved changes?") Then Return
        DeleteUnsavedFiles()
        _loading = True
        _name.Text = _oName
        _cat.SelectedId = _oCat
        _contact.Text = _oContact
        _desc.Value = _oDesc
        _loading = False
        _logoBox.SetSeed(_oName)
        _coverBox.SetSeed(_oName & "cover")
        SetPhoto(True, _oLogo)
        SetPhoto(False, _oCover)
        UpdatePreview()
        RefreshState()
    End Sub

    Private Sub DeleteUnsavedFiles()
        For Each rel As String In _staged
            If rel <> _oLogo AndAlso rel <> _oCover Then StoreSvc.DeleteStallImage(rel)
        Next
        _staged.Clear()
    End Sub

    Private Sub OnSales(sender As Object, e As EventArgs)
        Using f As New SalesRecordsForm(_stallId, If(_oName = "", "Stall", _oName))
            f.ShowDialog(FindForm())
        End Using
    End Sub

    ' ---------------- layout ----------------

    Protected Overrides Sub LayoutPage(contentWidth As Integer)
        Dim cw As Integer = contentWidth
        _accent.SetBounds(M, 14, 46, 5)
        _title.Location = New Point(M, 26)
        _salesBtn.Location = New Point(M + cw - _salesBtn.Width, 34)
        _subtitle.Location = New Point(M, 78)
        Dim y As Integer = 78 + _subtitle.Height + 24
        If _failed Then
            Ui.FitLabel(_error, Math.Min(cw, 700))
            _error.Location = New Point(M, y)
            SetContentHeight(y + _error.Height + 40)
            Return
        End If

        Dim twoColumns As Boolean = (cw >= 1000)
        Dim rightW As Integer = If(twoColumns, 440, cw)
        Dim leftW As Integer = If(twoColumns, cw - rightW - 24, cw)

        Dim photosH As Integer = LayoutPhotos(M, y, leftW)
        Dim detailsH As Integer = LayoutDetails(M, y + photosH + 22, leftW)
        Dim leftBottom As Integer = y + photosH + 22 + detailsH

        Dim rx As Integer = If(twoColumns, M + leftW + 24, M)
        Dim ry As Integer = If(twoColumns, y, leftBottom + 22)
        Dim rightH As Integer = LayoutPreview(rx, ry, rightW)
        Dim bottom As Integer = Math.Max(leftBottom, ry + rightH)
        SetContentHeight(bottom + 40)
    End Sub

    Private Function LayoutGroup(box As Control, bw As Integer, bh As Integer, title As Label, hint As Label, change As Control, remove As Control,
                                 gx As Integer, gy As Integer, gw As Integer) As Integer
        box.SetBounds(gx, gy, bw, bh)
        Dim tx As Integer = gx + bw + 20
        Dim tw As Integer = Math.Max(120, gw - bw - 20)
        title.Location = New Point(tx, gy)
        Ui.FitLabel(hint, tw)
        hint.Location = New Point(tx, gy + 30)
        Dim by As Integer = gy + 30 + hint.Height + 12
        change.Location = New Point(tx, by)
        remove.Location = New Point(tx + change.Width + 10, by)
        Return Math.Max(bh, by + 42 - gy)
    End Function

    Private Function LayoutPhotos(x As Integer, y As Integer, w As Integer) As Integer
        Dim inner As Integer = w - 56
        Dim two As Boolean = (inner >= 640)
        Dim gw As Integer = If(two, (inner - 28) \ 2, inner)
        Dim top As Integer = 68
        _photosTitle.Location = New Point(28, 22)
        Dim coverW As Integer = Math.Min(200, Math.Max(120, (gw - 20) \ 2))
        Dim h1 As Integer = LayoutGroup(_logoBox, 112, 112, _logoTitle, _logoHint, _logoChange, _logoRemove, 28, top, gw)
        Dim total As Integer
        If two Then
            Dim h2 As Integer = LayoutGroup(_coverBox, coverW, 112, _coverTitle, _coverHint, _coverChange, _coverRemove, 28 + gw + 28, top, gw)
            total = Math.Max(h1, h2)
        Else
            Dim h2 As Integer = LayoutGroup(_coverBox, coverW, 112, _coverTitle, _coverHint, _coverChange, _coverRemove, 28, top + h1 + 24, gw)
            total = h1 + 24 + h2
        End If
        _photos.SetBounds(x, y, w, top + total + 28)
        Return _photos.Height
    End Function

    Private Function LayoutDetails(x As Integer, y As Integer, w As Integer) As Integer
        Dim fw As Integer = w - 56
        Dim cy As Integer = 22
        _detailsTitle.Location = New Point(28, cy)
        cy += 48
        _lblName.Location = New Point(28, cy)
        cy += 26
        _name.SetBounds(28, cy, fw, 54)
        cy += 54 + 18
        _lblCat.Location = New Point(28, cy)
        cy += 26
        _cat.SetBounds(28, cy, fw, 54)
        cy += 54 + 8
        Ui.FitLabel(_catHint, fw)
        _catHint.Location = New Point(28, cy)
        cy += _catHint.Height + 16
        _lblDesc.Location = New Point(28, cy)
        cy += 26
        _desc.SetBounds(28, cy, fw, 104)
        cy += 104 + 18
        _lblContact.Location = New Point(28, cy)
        cy += 26
        _contact.SetBounds(28, cy, fw, 54)
        cy += 54 + 6
        _contactErr.Location = New Point(30, cy)
        cy += 30
        _save.Location = New Point(28, cy)
        _discard.Location = New Point(28 + _save.Width + 12, cy)
        _info.Location = New Point(28 + _save.Width + 12 + _discard.Width + 16, cy + (48 - _info.Height) \ 2)
        cy += 48 + 28
        _details.SetBounds(x, y, w, cy)
        Return cy
    End Function

    Private Function LayoutPreview(x As Integer, y As Integer, w As Integer) As Integer
        _prevInnerW = Math.Max(200, w - 48)
        _prevTitle.Location = New Point(24, 22)
        Ui.FitLabel(_prevSub, _prevInnerW)
        _prevSub.Location = New Point(24, 54)
        _cap1.Location = New Point(24, 96)
        If _homeCard IsNot Nothing Then _homeCard.SetBounds(24, 120, _prevInnerW, 214)
        _cap2.Location = New Point(24, 354)
        _banner.SetBounds(24, 380, _prevInnerW, 140)
        _preview.SetBounds(x, y, w, 540)
        Return 540
    End Function

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            _poll.Stop()
            _poll.Dispose()
            Try
                DeleteUnsavedFiles()
            Catch
            End Try
            If _logoImg IsNot Nothing Then _logoImg.Dispose()
            If _coverImg IsNot Nothing Then _coverImg.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class

''' <summary>Logo (circle) or cover (rounded rectangle) preview; shows coloured initials when there is no photo.</summary>
Public Class PhotoBox
    Inherits Control

    Public Circle As Boolean
    Public Seed As String = ""
    Private _img As Image
    Private _bmp As Bitmap

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, False)
        BackColor = Color.White
    End Sub

    Public Sub SetImage(img As Image)
        _img = img
        DropCache()
        Invalidate()
    End Sub

    Public Sub SetSeed(s As String)
        Seed = s
        If _img Is Nothing Then
            DropCache()
            Invalidate()
        End If
    End Sub

    Private Sub DropCache()
        If _bmp IsNot Nothing Then
            _bmp.Dispose()
            _bmp = Nothing
        End If
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        DropCache()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        If Width < 8 OrElse Height < 8 Then Return
        g.SmoothingMode = SmoothingMode.AntiAlias
        If _bmp Is Nothing Then _bmp = Painting.RenderCover(_img, Width - 1, Height - 1, If(Seed = "", "Store", Seed))
        Using tb As New TextureBrush(_bmp), pen As New Pen(Theme.Border, 1.0F)
            If Circle Then
                g.FillEllipse(tb, 0, 0, Width - 2, Height - 2)
                g.DrawEllipse(pen, 0, 0, Width - 2, Height - 2)
            Else
                Using p As GraphicsPath = Theme.RoundRectPath(New Rectangle(0, 0, Width - 2, Height - 2), 16)
                    g.FillPath(tb, p)
                    g.DrawPath(pen, p)
                End Using
            End If
        End Using
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then DropCache()
        MyBase.Dispose(disposing)
    End Sub
End Class

''' <summary>Dropdown that only offers a fixed list (the stall categories).</summary>
Public Class StoreCombo
    Inherits Control

    Public Event SelectionChanged As EventHandler

    Private ReadOnly _names As New List(Of String)
    Private ReadOnly _ids As New List(Of Integer)
    Private ReadOnly _f As Font = Theme.F(11)
    Private _sel As Integer = -1
    Private _menu As ContextMenuStrip
    Private _hover As Boolean

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, False)
        BackColor = Color.White
        Cursor = Cursors.Hand
    End Sub

    Public Sub SetItems(names As List(Of String), ids As List(Of Integer))
        _names.Clear()
        _names.AddRange(names)
        _ids.Clear()
        _ids.AddRange(ids)
        _sel = -1
        Invalidate()
    End Sub

    Public Property SelectedId As Integer
        Get
            Return If(_sel >= 0 AndAlso _sel < _ids.Count, _ids(_sel), 0)
        End Get
        Set(value As Integer)
            _sel = _ids.IndexOf(value)
            Invalidate()
        End Set
    End Property

    Public ReadOnly Property SelectedName As String
        Get
            Return If(_sel >= 0 AndAlso _sel < _names.Count, _names(_sel), "")
        End Get
    End Property

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Using p As GraphicsPath = Theme.RoundRectPath(New Rectangle(0, 0, Width - 1, Height - 1), 16), b As New SolidBrush(Theme.FieldBg),
              pen As New Pen(If(_hover, Theme.Primary, Theme.Border), If(_hover, 1.6F, 1.0F))
            g.FillPath(b, p)
            g.DrawPath(pen, p)
        End Using
        Dim txt As String = If(SelectedName = "", "Select a category", SelectedName)
        TextRenderer.DrawText(g, txt, _f, New Rectangle(20, 0, Width - 70, Height), If(SelectedName = "", Theme.TextMuted, Theme.TextDark),
                              TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)
        Icons.Draw(g, "chev_d", New Rectangle(Width - 38, (Height - 18) \ 2, 18, 18), Theme.TextMuted)
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

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        If e.Button <> MouseButtons.Left Then Return
        If _menu IsNot Nothing Then _menu.Dispose()
        _menu = New ContextMenuStrip()
        _menu.Font = Theme.F(10.5F)
        _menu.MinimumSize = New Size(Width, 0)
        For i As Integer = 0 To _names.Count - 1
            Dim it As ToolStripItem = _menu.Items.Add(_names(i))
            it.Tag = i
            AddHandler it.Click, AddressOf OnPick
        Next
        _menu.Show(Me, New Point(0, Height + 4))
    End Sub

    Private Sub OnPick(sender As Object, e As EventArgs)
        Dim i As Integer = CInt(DirectCast(sender, ToolStripItem).Tag)
        If i = _sel Then Return
        _sel = i
        Invalidate()
        RaiseEvent SelectionChanged(Me, EventArgs.Empty)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing AndAlso _menu IsNot Nothing Then _menu.Dispose()
        MyBase.Dispose(disposing)
    End Sub
End Class

''' <summary>"On your stall page" preview: soft gradient banner with logo, name, category and open status.</summary>
Public Class StallBannerPreview
    Inherits Control

    Private _name As String = ""
    Private _category As String = ""
    Private _open As Boolean
    Private _logo As Bitmap

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, False)
        BackColor = Color.White
    End Sub

    Public Sub Setup(name As String, category As String, logoImg As Image, isOpen As Boolean)
        _name = name
        _category = category
        _open = isOpen
        If _logo IsNot Nothing Then _logo.Dispose()
        _logo = Painting.RenderCover(logoImg, 72, 72, name)
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        If Width < 120 OrElse Height < 80 Then Return
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim r As New Rectangle(0, 0, Width - 1, Height - 1)
        Using p As GraphicsPath = Theme.RoundRectPath(r, 20), lg As New LinearGradientBrush(r, Color.FromArgb(255, 245, 239), Color.FromArgb(254, 226, 210), LinearGradientMode.Horizontal)
            g.FillPath(lg, p)
        End Using
        Dim lx As Integer = 26
        Dim ly As Integer = (Height - 72) \ 2
        Using wb As New SolidBrush(Color.White)
            g.FillEllipse(wb, lx - 4, ly - 4, 80, 80)
        End Using
        If _logo IsNot Nothing Then
            Using tb As New TextureBrush(_logo)
                tb.TranslateTransform(lx, ly)
                g.FillEllipse(tb, lx, ly, 72, 72)
            End Using
        End If
        Dim tx As Integer = lx + 72 + 22
        TextRenderer.DrawText(g, _name, Theme.F(17, True), New Rectangle(tx, ly - 6, Width - tx - 16, 32), Theme.TextDark,
                              TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix Or TextFormatFlags.VerticalCenter)
        TextRenderer.DrawText(g, _category, Theme.F(11.5F), New Rectangle(tx, ly + 26, Width - tx - 16, 22), Theme.TextMuted,
                              TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix Or TextFormatFlags.VerticalCenter)
        Dim st As String = If(_open, "Open", "Closed")
        Dim pf As Font = Theme.F(10, True)
        Dim pw As Integer = TextRenderer.MeasureText(st, pf, New Size(200, 40), TextFormatFlags.NoPadding).Width + 28
        Dim pr As New Rectangle(tx, ly + 54, pw, 26)
        Using p As GraphicsPath = Theme.RoundRectPath(pr, 13), b As New SolidBrush(If(_open, Theme.SuccessSoft, Theme.DangerSoft))
            g.FillPath(b, p)
        End Using
        TextRenderer.DrawText(g, st, pf, pr, If(_open, Theme.Success, Theme.Danger), TextFormatFlags.NoPadding Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing AndAlso _logo IsNot Nothing Then _logo.Dispose()
        MyBase.Dispose(disposing)
    End Sub
End Class
