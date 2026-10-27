Imports System.Data
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.IO
Imports System.Windows.Forms

''' <summary>Right-hand panel of the seller Menu tab: add a new item or edit the selected one.</summary>
Public Class ItemEditPanel
    Inherits Control

    Public Const PanelW As Integer = 440
    Private Const X As Integer = 24
    Private Const CW As Integer = 372

    Public Event Saved(productId As Integer)
    Public Event Deleted()
    Public Event CloseRequested()
    Public Event ManageExtrasRequested()

    Private Class VariantRow
        Public Id As Integer
        Public NameBox As AppTextBox
        Public PriceBox As AppTextBox
        Public Del As IconButton
    End Class

    Private ReadOnly _stallId As Integer
    Private _productId As Integer
    Private _imagePath As String = ""
    Private _origImagePath As String = ""
    Private _pendingFile As String = ""
    Private _hasImage As Boolean
    Private _loading As Boolean

    Private ReadOnly _title As Label
    Private ReadOnly _close As New IconButton()
    Private ReadOnly _body As New Panel()
    Private ReadOnly _save As RoundedButton
    Private ReadOnly _delete As Label

    Private ReadOnly _img As New CoverImage()
    Private ReadOnly _imgHint As Label
    Private ReadOnly _imgPick As Label
    Private ReadOnly _imgRemove As Label
    Private ReadOnly _nameCap As Label
    Private ReadOnly _name As AppTextBox
    Private ReadOnly _descCap As Label
    Private ReadOnly _desc As New AreaField()
    Private ReadOnly _catCap As Label
    Private ReadOnly _cat As New CategoryField()
    Private ReadOnly _priceCap As Label
    Private ReadOnly _price As AppTextBox
    Private ReadOnly _priceHint As Label
    Private ReadOnly _varCap As Label
    Private ReadOnly _varHint As Label
    Private ReadOnly _addVar As RoundedButton
    Private ReadOnly _rows As New List(Of VariantRow)
    Private ReadOnly _maxCap As Label
    Private ReadOnly _max As AppTextBox
    Private ReadOnly _extraCap As Label
    Private ReadOnly _manage As Label
    Private ReadOnly _extraFlow As New ChipFlow()
    Private ReadOnly _extraEmpty As Label
    Private ReadOnly _extraIds As New List(Of Integer)
    Private ReadOnly _remCap As Label
    Private ReadOnly _remHint As Label
    Private ReadOnly _remFlow As New ChipFlow()
    Private ReadOnly _remBox As AppTextBox
    Private ReadOnly _remAdd As RoundedButton
    Private ReadOnly _stockCap As Label
    Private ReadOnly _stockHint As Label
    Private ReadOnly _stockSwitch As New ToggleSwitch()
    Private ReadOnly _stockQtyCap As Label
    Private ReadOnly _stockQty As AppTextBox
    Private ReadOnly _lowCap As Label
    Private ReadOnly _low As AppTextBox

    Public Sub New(stallId As Integer)
        _stallId = stallId
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        BackColor = Theme.Bg

        _title = Ui.Lbl("Add item", 17, True)
        _title.BackColor = Color.White
        _close.IconName = "x"
        _close.IconColor = Theme.TextMuted
        _close.BorderColor = Theme.Border
        _close.HoverBack = Theme.PrimarySoft
        _close.Size = New Size(40, 40)
        _close.BackColor = Color.White
        AddHandler _close.Click, AddressOf OnCloseClick

        _body.AutoScroll = True
        _body.BackColor = Color.White

        _save = Ui.PrimaryBtn("Add item", 200, 52)
        _save.Radius = 16
        _save.Font = Theme.F(11.5F, True)
        _save.BackColor = Color.White
        AddHandler _save.Click, AddressOf OnSave
        _delete = Ui.Link("Delete item", 10, Theme.Danger, False)
        _delete.BackColor = Color.White
        AddHandler _delete.Click, AddressOf OnDelete

        _img.Radius = 18
        _img.PlaceholderText = "Item"
        _img.Cursor = Cursors.Hand
        AddHandler _img.Click, AddressOf OnPickImage
        _imgHint = Ui.WrapLbl("Customers see this first. A clear photo helps sales.", 9.5F, CW, Theme.TextMuted)
        _imgPick = Ui.Link("Upload photo", 10)
        AddHandler _imgPick.Click, AddressOf OnPickImage
        _imgRemove = Ui.Link("Remove", 10, Theme.Danger, False)
        AddHandler _imgRemove.Click, AddressOf OnRemoveImage

        _nameCap = Cap("Item name *")
        _name = MakeBox("e.g. Classic Milk Tea", 80)
        _descCap = Cap("Description (optional)")
        _catCap = Cap("Category *")
        _priceCap = Cap("Price *")
        _price = MakeBox("0.00", 9)
        MenuFmt.NumericOnly(_price, True)
        _priceHint = Ui.WrapLbl("This item has variants, so every variant has its own price. The lowest one shows as From.", 9.5F, CW, Theme.TextMuted)

        _varCap = Cap("Variants (optional)")
        _varHint = Ui.WrapLbl("Add sizes or options with their own price, for example Medium and Large.", 9.5F, CW, Theme.TextMuted)
        _addVar = Ui.SoftBtn("+ Add variant", 170, 42)
        _addVar.BackColor = Color.White
        AddHandler _addVar.Click, AddressOf OnAddVariant

        _maxCap = Cap("Max per order (optional)")
        _max = MakeBox("No limit", 2)
        MenuFmt.NumericOnly(_max, False)

        _extraCap = Cap("Extras (optional)")
        _manage = Ui.Link("Manage extras", 10)
        AddHandler _manage.Click, AddressOf OnManageExtras
        _extraFlow.Mode = ChipMode.Toggle
        _extraFlow.BackColor = Color.White
        _extraEmpty = Ui.WrapLbl("No extras yet. Use Manage extras to create some, then tick the ones this item offers.", 9.5F, CW, Theme.TextMuted)

        _remCap = Cap("Can be removed (optional)")
        _remHint = Ui.WrapLbl("Ingredients a customer can ask to leave out, for example Onions or Ice.", 9.5F, CW, Theme.TextMuted)
        _remFlow.Mode = ChipMode.Tags
        _remFlow.BackColor = Color.White
        _remBox = MakeBox("Add an ingredient", 40)
        _remAdd = Ui.SoftBtn("Add", 76, 46)
        _remAdd.BackColor = Color.White
        AddHandler _remAdd.Click, AddressOf OnAddRemovable
        AddHandler _remBox.EnterPressed, AddressOf OnAddRemovable
        AddHandler _remFlow.TagRemoved, AddressOf OnTagRemoved

        _stockCap = Cap("Track stock")
        _stockHint = Ui.WrapLbl("Show how many are left. The item shows Sold out at 0.", 9.5F, CW - 70, Theme.TextMuted)
        _stockSwitch.BackColor = Color.White
        AddHandler _stockSwitch.CheckedChanged, AddressOf OnStockToggled
        _stockQtyCap = Cap("Stock quantity")
        _stockQty = MakeBox("0", 5)
        MenuFmt.NumericOnly(_stockQty, False)
        _lowCap = Cap("Low stock alert at")
        _low = MakeBox("10", 5)
        MenuFmt.NumericOnly(_low, False)

        For Each c As Control In New Control() {_img, _imgHint, _imgPick, _imgRemove, _nameCap, _name, _descCap, _desc, _catCap, _cat, _priceCap, _price, _priceHint,
                                                _varCap, _varHint, _addVar, _maxCap, _max, _extraCap, _manage, _extraFlow, _extraEmpty, _remCap, _remHint, _remFlow,
                                                _remBox, _remAdd, _stockCap, _stockHint, _stockSwitch, _stockQtyCap, _stockQty, _lowCap, _low}
            _body.Controls.Add(c)
        Next
        Controls.Add(_title)
        Controls.Add(_close)
        Controls.Add(_body)
        Controls.Add(_save)
        Controls.Add(_delete)

        LoadExtrasLibrary()
        StartNew("")
    End Sub

    Private Function Cap(text As String) As Label
        Return Ui.Lbl(text, 10.5F, True)
    End Function

    Private Function MakeBox(placeholder As String, maxLen As Integer) As AppTextBox
        Dim b As New AppTextBox()
        b.Font = Theme.F(11)
        b.PlaceholderText = placeholder
        b.MaxLength = maxLen
        Return b
    End Function

    ' ---------------------------------------------------------------- public API
    Public Sub SetCategories(names As List(Of String))
        _cat.Categories.Clear()
        _cat.Categories.AddRange(names)
    End Sub

    ''' <summary>Re-read the stall's extras library (after Manage extras) and keep the ticks that still exist.</summary>
    Public Sub ReloadExtras()
        Dim keep As New List(Of Integer)
        For Each i As Integer In _extraFlow.Selected
            If i < _extraIds.Count Then keep.Add(_extraIds(i))
        Next
        LoadExtrasLibrary()
        SelectExtras(keep)
        RelayoutBody()
    End Sub

    Public Sub StartNew(presetCategory As String)
        ResetFields()
        _productId = 0
        _title.Text = "Add item"
        _save.Text = "Add item"
        _delete.Visible = False
        _cat.Value = presetCategory
        RelayoutBody()
        _body.AutoScrollPosition = New Point(0, 0)
    End Sub

    Public Sub LoadItem(productId As Integer)
        Dim p As DataRow = Nothing
        Try
            p = SellerSvc.GetProduct(productId, _stallId)
        Catch ex As Exception
            Toast.Show(Me, "Could not load the item: " & ex.Message, True)
            Return
        End Try
        If p Is Nothing Then
            StartNew("")
            Return
        End If
        ResetFields()
        _loading = True
        _productId = productId
        _title.Text = "Edit item"
        _save.Text = "Save changes"
        _delete.Visible = True
        _name.Text = Db.GetStr(p, "name")
        Dim d As String = Db.GetStr(p, "description")
        If d = "" Then d = Db.GetStr(p, "short_desc")
        _desc.Value = d
        _cat.Value = Db.GetStr(p, "category")
        _price.Text = MenuFmt.FormatNum(Db.GetDec(p, "base_price"))
        For Each r As DataRow In CatalogSvc.GetSizes(productId).Rows
            AddRow(Db.GetInt(r, "id"), Db.GetStr(r, "name"), MenuFmt.FormatNum(Db.GetDec(r, "price")))
        Next
        Dim mx As Integer = Db.GetInt(p, "max_per_order")
        _max.Text = If(mx > 0, mx.ToString(), "")
        SelectExtras(SellerSvc.GetLinkedExtraIds(productId))
        For Each nm As String In SellerSvc.GetRemovables(productId)
            _remFlow.Items.Add(nm)
        Next
        _stockSwitch.Checked = Db.GetBool(p, "track_stock")
        _stockQty.Text = Db.GetInt(p, "stock").ToString()
        _low.Text = Db.GetInt(p, "low_stock_at").ToString()
        _imagePath = Db.GetStr(p, "image_path")
        _origImagePath = _imagePath
        SetImage(Ui.LoadImage(_imagePath))
        _loading = False
        RelayoutBody()
        _body.AutoScrollPosition = New Point(0, 0)
    End Sub

    ' ---------------------------------------------------------------- state helpers
    Private Sub ResetFields()
        _loading = True
        _name.Text = ""
        _desc.Value = ""
        _cat.Value = ""
        _price.Text = ""
        _max.Text = ""
        _stockQty.Text = ""
        _low.Text = "10"
        _remBox.Text = ""
        ClearRows()
        _remFlow.Items.Clear()
        _extraFlow.Selected.Clear()
        _stockSwitch.Checked = False
        _pendingFile = ""
        _imagePath = ""
        _origImagePath = ""
        SetImage(Nothing)
        _loading = False
    End Sub

    Private Sub SetImage(img As Image)
        _img.Image = img
        _hasImage = (img IsNot Nothing)
        _imgPick.Text = If(_hasImage, "Change photo", "Upload photo")
    End Sub

    Private Sub LoadExtrasLibrary()
        _extraIds.Clear()
        _extraFlow.Items.Clear()
        _extraFlow.Selected.Clear()
        Try
            For Each r As DataRow In SellerSvc.GetStallExtras(_stallId).Rows
                _extraIds.Add(Db.GetInt(r, "id"))
                _extraFlow.Items.Add(Db.GetStr(r, "name") & "  +" & OrderCard.Money(Db.GetDec(r, "price")))
            Next
        Catch
        End Try
        _extraFlow.Invalidate()
    End Sub

    Private Sub SelectExtras(ids As List(Of Integer))
        _extraFlow.Selected.Clear()
        For i As Integer = 0 To _extraIds.Count - 1
            If ids.Contains(_extraIds(i)) Then _extraFlow.Selected.Add(i)
        Next
        _extraFlow.Invalidate()
    End Sub

    Private Sub ClearRows()
        For Each r As VariantRow In _rows
            For Each c As Control In New Control() {r.NameBox, r.PriceBox, r.Del}
                _body.Controls.Remove(c)
                c.Dispose()
            Next
        Next
        _rows.Clear()
    End Sub

    Private Function AddRow(id As Integer, name As String, price As String) As VariantRow
        Dim r As New VariantRow()
        r.Id = id
        r.NameBox = MakeBox("e.g. Large", 30)
        r.NameBox.Text = name
        r.PriceBox = MakeBox("Price", 9)
        MenuFmt.NumericOnly(r.PriceBox, True)
        r.PriceBox.Text = price
        r.Del = New IconButton()
        r.Del.IconName = "x"
        r.Del.IconColor = Theme.TextMuted
        r.Del.BorderColor = Theme.Border
        r.Del.HoverBack = Theme.DangerSoft
        r.Del.Size = New Size(44, 44)
        r.Del.BackColor = Color.White
        r.Del.Tag = r
        AddHandler r.Del.Click, AddressOf OnRemoveVariant
        _body.Controls.Add(r.NameBox)
        _body.Controls.Add(r.PriceBox)
        _body.Controls.Add(r.Del)
        _rows.Add(r)
        Return r
    End Function

    ' ---------------------------------------------------------------- layout
    Private Function PlaceField(cap As Label, ctl As Control, y As Integer, h As Integer) As Integer
        cap.Location = New Point(X, y)
        y += 26
        ctl.SetBounds(X, y, CW, h)
        Return y + h + 18
    End Function

    Private Sub RelayoutBody()
        Dim pos As Integer = -_body.AutoScrollPosition.Y
        _body.AutoScrollPosition = New Point(0, 0)
        _body.SuspendLayout()
        Dim y As Integer = 6

        _img.SetBounds(X, y, CW, 250)
        y += 250 + 10
        _imgHint.Location = New Point(X, y)
        y += _imgHint.Height + 4
        _imgPick.Location = New Point(X, y)
        _imgRemove.Location = New Point(X + _imgPick.PreferredWidth + 18, y)
        _imgRemove.Visible = _hasImage
        y += 34

        y = PlaceField(_nameCap, _name, y, 48)
        y = PlaceField(_descCap, _desc, y, 96)
        y = PlaceField(_catCap, _cat, y, 48)

        _priceCap.Location = New Point(X, y)
        y += 26
        If _rows.Count = 0 Then
            _price.Visible = True
            _priceHint.Visible = False
            _price.SetBounds(X, y, CW, 48)
            y += 48 + 18
        Else
            _price.Visible = False
            _priceHint.Visible = True
            _priceHint.Location = New Point(X, y)
            y += _priceHint.Height + 18
        End If

        _varCap.Location = New Point(X, y)
        y += 26
        _varHint.Location = New Point(X, y)
        y += _varHint.Height + 8
        For Each r As VariantRow In _rows
            r.NameBox.SetBounds(X, y, 184, 46)
            r.PriceBox.SetBounds(X + 192, y, 120, 46)
            r.Del.SetBounds(X + 320, y + 1, 44, 44)
            y += 54
        Next
        _addVar.SetBounds(X, y, 170, 42)
        y += 42 + 18

        y = PlaceField(_maxCap, _max, y, 48)

        _extraCap.Location = New Point(X, y)
        _manage.Location = New Point(X + CW - _manage.PreferredWidth, y + 2)
        y += 26
        If _extraFlow.Items.Count = 0 Then
            _extraFlow.Visible = False
            _extraEmpty.Visible = True
            _extraEmpty.Location = New Point(X, y)
            y += _extraEmpty.Height + 18
        Else
            _extraEmpty.Visible = False
            _extraFlow.Visible = True
            Dim fh As Integer = _extraFlow.RequiredHeight(CW)
            _extraFlow.SetBounds(X, y, CW, fh)
            y += fh + 18
        End If

        _remCap.Location = New Point(X, y)
        y += 26
        _remHint.Location = New Point(X, y)
        y += _remHint.Height + 8
        If _remFlow.Items.Count > 0 Then
            Dim rh As Integer = _remFlow.RequiredHeight(CW)
            _remFlow.Visible = True
            _remFlow.SetBounds(X, y, CW, rh)
            y += rh + 10
        Else
            _remFlow.Visible = False
        End If
        _remBox.SetBounds(X, y, CW - 84, 46)
        _remAdd.SetBounds(X + CW - 76, y, 76, 46)
        y += 46 + 22

        _stockCap.Location = New Point(X, y)
        _stockHint.Location = New Point(X, y + 24)
        _stockSwitch.Location = New Point(X + CW - 54, y + 4)
        y += 24 + _stockHint.Height + 16
        If _stockSwitch.Checked Then
            _stockQtyCap.Visible = True
            _stockQty.Visible = True
            y = PlaceField(_stockQtyCap, _stockQty, y, 48)
            _lowCap.Visible = True
            _low.Visible = True
            y = PlaceField(_lowCap, _low, y, 48)
        Else
            _stockQtyCap.Visible = False
            _stockQty.Visible = False
            _lowCap.Visible = False
            _low.Visible = False
        End If

        _body.AutoScrollMinSize = New Size(0, y + 10)
        _body.ResumeLayout(True)
        _body.AutoScrollPosition = New Point(0, pos)
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        If _body Is Nothing Then Return
        _title.Location = New Point(24, 24)
        _close.Location = New Point(Width - 24 - 40, 18)
        _body.SetBounds(1, 70, Math.Max(50, Width - 3), Math.Max(100, Height - 70 - 104))
        _save.SetBounds(24, Height - 98, Math.Max(100, Width - 48), 52)
        _delete.Location = New Point((Width - _delete.PreferredWidth) \ 2, Height - 38)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Painting.DrawCardShadow(g, Width, Height, 22)
        Using p As GraphicsPath = Theme.RoundRectPath(New Rectangle(0, 0, Width - 1, Height - 5), 22)
            g.FillPath(Brushes.White, p)
        End Using
    End Sub

    ' ---------------------------------------------------------------- events
    Private Sub OnCloseClick(sender As Object, e As EventArgs)
        RaiseEvent CloseRequested()
    End Sub

    Private Sub OnManageExtras(sender As Object, e As EventArgs)
        RaiseEvent ManageExtrasRequested()
    End Sub

    Private Sub OnStockToggled(sender As Object, e As EventArgs)
        If _loading Then Return
        RelayoutBody()
    End Sub

    Private Sub OnPickImage(sender As Object, e As EventArgs)
        Dim f As String = MenuFmt.PickImage(FindForm())
        If f = "" Then Return
        Try
            Dim img As Image = Image.FromStream(New MemoryStream(File.ReadAllBytes(f)))
            SetImage(img)
            _pendingFile = f
            RelayoutBody()
        Catch
            Toast.Show(Me, "That file is not a valid picture.", True)
        End Try
    End Sub

    Private Sub OnRemoveImage(sender As Object, e As EventArgs)
        SetImage(Nothing)
        _pendingFile = ""
        _imagePath = ""
        RelayoutBody()
    End Sub

    Private Sub OnAddVariant(sender As Object, e As EventArgs)
        If _rows.Count >= 10 Then
            Toast.Show(Me, "An item can have up to 10 variants.", True)
            Return
        End If
        Dim r As VariantRow = AddRow(0, "", "")
        RelayoutBody()
        _body.ScrollControlIntoView(r.NameBox)
        r.NameBox.Focus()
    End Sub

    Private Sub OnRemoveVariant(sender As Object, e As EventArgs)
        Dim r As VariantRow = DirectCast(DirectCast(sender, Control).Tag, VariantRow)
        For Each c As Control In New Control() {r.NameBox, r.PriceBox, r.Del}
            _body.Controls.Remove(c)
            c.Dispose()
        Next
        _rows.Remove(r)
        RelayoutBody()
    End Sub

    Private Sub OnAddRemovable(sender As Object, e As EventArgs)
        Dim t As String = _remBox.Text.Trim()
        If t = "" Then Return
        For Each existing As String In _remFlow.Items
            If String.Equals(existing, t, StringComparison.OrdinalIgnoreCase) Then
                Toast.Show(Me, "That ingredient is already listed.", True)
                Return
            End If
        Next
        If _remFlow.Items.Count >= 15 Then
            Toast.Show(Me, "You can list up to 15 removable ingredients.", True)
            Return
        End If
        _remFlow.Items.Add(t)
        _remBox.Text = ""
        RelayoutBody()
    End Sub

    Private Sub OnTagRemoved(index As Integer)
        If index >= 0 AndAlso index < _remFlow.Items.Count Then
            _remFlow.Items.RemoveAt(index)
            RelayoutBody()
        End If
    End Sub

    Private Sub Fail(msg As String, ctl As Control)
        Toast.Show(Me, msg, True)
        If ctl IsNot Nothing AndAlso ctl.Visible Then
            _body.ScrollControlIntoView(ctl)
            ctl.Focus()
        End If
    End Sub

    ' ---------------------------------------------------------------- save / delete
    Private Sub OnSave(sender As Object, e As EventArgs)
        Dim d As New ProductDraft()
        d.Id = _productId
        d.StallId = _stallId

        d.Name = _name.Text.Trim()
        If d.Name = "" Then
            Fail("Enter the item name.", _name)
            Return
        End If
        d.Description = _desc.Value.Trim()
        d.Category = _cat.Value
        If d.Category = "" Then
            Fail("Choose or type a category.", _cat)
            Return
        End If

        If _rows.Count = 0 Then
            Dim pr As Decimal
            If Not MenuFmt.TryPrice(_price.Text, pr) OrElse pr <= 0 Then
                Fail("Enter a price greater than 0.", _price)
                Return
            End If
            d.Price = pr
        Else
            Dim names As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            For Each r As VariantRow In _rows
                Dim z As New SizeRow()
                z.Id = r.Id
                z.Name = r.NameBox.Text.Trim()
                If z.Name = "" Then
                    Fail("Every variant needs a name.", r.NameBox)
                    Return
                End If
                If Not names.Add(z.Name) Then
                    Fail("Variant names must be different: " & z.Name, r.NameBox)
                    Return
                End If
                Dim vp As Decimal
                If Not MenuFmt.TryPrice(r.PriceBox.Text, vp) OrElse vp <= 0 Then
                    Fail("Enter a price greater than 0 for " & z.Name & ".", r.PriceBox)
                    Return
                End If
                z.Price = vp
                d.Sizes.Add(z)
            Next
        End If

        If _max.Text.Trim() <> "" Then
            Dim mx As Integer
            If Not MenuFmt.TryInt(_max.Text, mx, 1, 99) Then
                Fail("Max per order must be a number from 1 to 99, or empty for no limit.", _max)
                Return
            End If
            d.MaxPerOrder = mx
        End If

        For Each i As Integer In _extraFlow.Selected
            If i < _extraIds.Count Then d.ExtraIds.Add(_extraIds(i))
        Next
        For Each nm As String In _remFlow.Items
            d.Removables.Add(nm)
        Next

        d.TrackStock = _stockSwitch.Checked
        If d.TrackStock Then
            Dim st As Integer
            If Not MenuFmt.TryInt(_stockQty.Text, st, 0, 99999) Then
                Fail("Enter the stock quantity (0 or more).", _stockQty)
                Return
            End If
            d.Stock = st
            Dim lo As Integer
            If Not MenuFmt.TryInt(_low.Text, lo, 0, 99999) Then
                Fail("Enter the low stock alert number (0 or more).", _low)
                Return
            End If
            d.LowStockAt = lo
        End If

        Dim storedNew As String = ""
        Try
            If _pendingFile <> "" Then
                storedNew = MenuFmt.StoreProductImage(_pendingFile)
                d.ImagePath = storedNew
            Else
                d.ImagePath = _imagePath
            End If
        Catch ex As Exception
            Toast.Show(Me, "Could not save the photo: " & ex.Message, True)
            Return
        End Try

        _save.Enabled = False
        Dim newId As Integer = 0
        Dim err As String = ""
        Dim ok As Boolean = SellerSvc.SaveProduct(d, newId, err)
        _save.Enabled = True
        If Not ok Then
            If storedNew <> "" Then MenuFmt.DeleteProductImage(storedNew)
            Toast.Show(Me, err, True)
            Return
        End If
        If _origImagePath <> "" AndAlso _origImagePath <> d.ImagePath Then MenuFmt.DeleteProductImage(_origImagePath)
        RaiseEvent Saved(newId)
    End Sub

    Private Sub OnDelete(sender As Object, e As EventArgs)
        If _productId = 0 Then Return
        If Not Ui.Confirm("Delete " & ChrW(&H201C) & _name.Text.Trim() & ChrW(&H201D) & " from your menu? This cannot be undone.") Then Return
        Try
            If SellerSvc.DeleteProduct(_productId, _stallId) Then
                If _origImagePath <> "" Then MenuFmt.DeleteProductImage(_origImagePath)
                RaiseEvent Deleted()
            Else
                Toast.Show(Me, "That item no longer exists.", True)
            End If
        Catch ex As Exception
            Toast.Show(Me, "Could not delete the item: " & ex.Message, True)
        End Try
    End Sub
End Class
