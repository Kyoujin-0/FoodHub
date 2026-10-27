Imports System.Data
Imports System.Drawing
Imports System.Windows.Forms

''' <summary>Product details: photo, description, size / dine option / extras, quantity, add to order and bookmark.</summary>
Public Class ProductDetailPage
    Inherits PageBase

    Private ReadOnly _shell As CustomerForm
    Private ReadOnly _backStall As Integer
    Private ReadOnly _p As DataRow
    Private ReadOnly _sizes As DataTable
    Private ReadOnly _extras As DataTable
    Private ReadOnly _available As Boolean
    Private ReadOnly _stallOpen As Boolean

    Private ReadOnly _back As Label
    Private ReadOnly _img As New CoverImage()
    Private ReadOnly _cat As Label
    Private ReadOnly _title As Label
    Private ReadOnly _avail As PillLabel
    Private ReadOnly _closed As PillLabel
    Private ReadOnly _stallLink As Label
    Private ReadOnly _rating As New RatingLabel()
    Private ReadOnly _desc As Label
    Private ReadOnly _price As Label
    Private ReadOnly _sizeCap As Label
    Private ReadOnly _sizeOpt As New OptionButtons()
    Private ReadOnly _dineCap As Label
    Private ReadOnly _dineOpt As New OptionButtons()
    Private ReadOnly _extraCap As Label
    Private ReadOnly _extraOpt As New OptionButtons()
    Private ReadOnly _bar As New RoundedPanel()
    Private ReadOnly _qty As New QtyStepper()
    Private ReadOnly _totalCap As Label
    Private ReadOnly _total As Label
    Private ReadOnly _add As RoundedButton
    Private ReadOnly _bookmark As New IconButton()
    Private _reviews As ReviewsSection

    Public Sub New(shell As CustomerForm, productId As Integer, backStallId As Integer)
        _shell = shell
        _backStall = backStallId
        _p = CatalogSvc.GetProduct(productId)
        If _p Is Nothing Then
            _shell.ShowHome()
            Return
        End If
        _sizes = CatalogSvc.GetSizes(productId)
        _extras = CatalogSvc.GetExtras(productId)
        _available = Db.GetBool(_p, "is_available")
        _stallOpen = Db.GetBool(_p, "is_open")

        _back = Ui.Link(ChrW(&H2190) & "  Back to menu", 10.5F)
        AddHandler _back.Click, Sub(s, e) _shell.ShowProducts(_backStall)

        _img.Radius = 26
        _img.Image = Ui.LoadImage(Db.GetStr(_p, "image_path"))
        _img.PlaceholderText = Db.GetStr(_p, "name")

        _cat = Ui.Lbl(Db.GetStr(_p, "menu_category").ToUpper(), 9, True, Theme.Primary)
        _title = Ui.Lbl(Db.GetStr(_p, "name"), 24, True)
        _title.AutoSize = False

        _avail = New PillLabel()
        _avail.Text = If(_available, "Available", "Unavailable")
        _avail.FillColor = If(_available, Theme.SuccessSoft, Theme.DangerSoft)
        _avail.TextColor = If(_available, Theme.Success, Theme.Danger)
        _avail.FitToText()
        _closed = New PillLabel()
        _closed.Text = "Stall closed"
        _closed.FillColor = Theme.WarningSoft
        _closed.TextColor = Theme.Warning
        _closed.FitToText()
        _closed.Visible = Not _stallOpen
        _stallLink = Ui.Link("Sold by " & Db.GetStr(_p, "stall_name"), 10)
        AddHandler _stallLink.Click, Sub(s, e) _shell.ShowProducts(Db.GetInt(_p, "stall_id"))
        _rating.SetRating(Db.GetDbl(_p, "avg_rating"), Db.GetInt(_p, "review_count"))
        _rating.Width = 190

        Dim d As String = Db.GetStr(_p, "description")
        If d = "" Then d = Db.GetStr(_p, "short_desc")
        _desc = Ui.Lbl(d, 10.5F, False, Theme.TextMuted)
        _desc.AutoSize = False
        _price = Ui.Lbl("", 26, True, Theme.Primary)

        _sizeCap = Ui.Lbl("Size", 10.5F, True)
        Dim sTitles As New List(Of String)
        Dim sSubs As New List(Of String)
        For Each r As DataRow In _sizes.Rows
            sTitles.Add(Db.GetStr(r, "name"))
            sSubs.Add(Theme.Peso(Db.GetDec(r, "price")))
        Next
        _sizeOpt.ItemWidth = 124
        _sizeOpt.ItemHeight = 60
        _sizeOpt.SetItems(sTitles, sSubs, If(sTitles.Count > 0, 0, -1))

        _dineCap = Ui.Lbl("Dining option", 10.5F, True)
        _dineOpt.ItemWidth = 150
        _dineOpt.ItemHeight = 48
        _dineOpt.SetItems(New String() {"Dine in", "Take out"}, New String() {"", ""}, 0)

        _extraCap = Ui.Lbl("Extras (optional)", 10.5F, True)
        Dim eTitles As New List(Of String)
        Dim eSubs As New List(Of String)
        For Each r As DataRow In _extras.Rows
            eTitles.Add(Db.GetStr(r, "name"))
            eSubs.Add("+ " & Theme.Peso(Db.GetDec(r, "price")))
        Next
        _extraOpt.MultiSelect = True
        _extraOpt.ItemWidth = 150
        _extraOpt.ItemHeight = 58
        _extraOpt.SetItems(eTitles, eSubs, -1)

        _bar.Radius = 18
        _bar.FillColor = Color.White
        _bar.SurroundColor = Theme.Bg
        _bar.Shadow = True
        _qty.Size = New Size(124, 42)
        _qty.Font = Theme.F(11, True)
        _totalCap = Ui.Lbl("Total", 9.5F, False, Theme.TextMuted)
        _total = Ui.Lbl("", 18, True, Theme.TextDark)
        _add = Ui.PrimaryBtn("Add to order", 190, 52)
        _add.IconName = "plus"
        _add.Radius = 16
        _add.Font = Theme.F(11, True)
        _bookmark.Size = New Size(52, 52)
        _bookmark.IconName = "bookmark"
        _bookmark.IconColor = Theme.TextMuted
        _bookmark.BorderColor = Theme.Border
        _bookmark.HoverBack = Theme.PrimarySoft
        _bar.Controls.Add(_qty)
        _bar.Controls.Add(_totalCap)
        _bar.Controls.Add(_total)
        _bar.Controls.Add(_add)
        _bar.Controls.Add(_bookmark)

        For Each c As Control In New Control() {_back, _img, _cat, _title, _avail, _closed, _stallLink, _rating, _desc, _price, _sizeCap, _sizeOpt, _dineCap, _dineOpt, _extraCap, _extraOpt, _bar}
            Controls.Add(c)
        Next
        _reviews = New ReviewsSection(productId, Db.GetStr(_p, "name"), Db.GetStr(_p, "stall_name"))
        AddHandler _reviews.DataChanged, Sub(avg, total) _rating.SetRating(avg, total)
        AddHandler _reviews.HeightChanged, Sub(s, e) RelayoutKeepScroll()
        Controls.Add(_reviews)

        _sizeCap.Visible = (_sizes.Rows.Count > 0)
        _sizeOpt.Visible = (_sizes.Rows.Count > 0)
        _extraCap.Visible = (_extras.Rows.Count > 0)
        _extraOpt.Visible = (_extras.Rows.Count > 0)

        AddHandler _sizeOpt.SelectionChanged, Sub(s, e) Recalc()
        AddHandler _dineOpt.SelectionChanged, Sub(s, e) Recalc()
        AddHandler _extraOpt.SelectionChanged, Sub(s, e) Recalc()
        AddHandler _qty.ValueChanged, Sub(s, e) Recalc()
        AddHandler _add.Click, AddressOf AddClicked
        AddHandler _bookmark.Click, AddressOf BookmarkClicked
        Recalc()
    End Sub

    Private Function SizeIndex() As Integer
        If _sizes.Rows.Count = 0 Then Return -1
        Return Math.Max(0, _sizeOpt.SelectedIndex)
    End Function

    Private Function SizeId() As Integer
        Dim i As Integer = SizeIndex()
        If i < 0 Then Return 0
        Return Db.GetInt(_sizes.Rows(i), "id")
    End Function

    Private Function UnitPrice() As Decimal
        Dim i As Integer = SizeIndex()
        If i < 0 Then Return Db.GetDec(_p, "base_price")
        Return Db.GetDec(_sizes.Rows(i), "price")
    End Function

    Private Function ExtraIds() As List(Of Integer)
        Dim l As New List(Of Integer)
        For Each i As Integer In _extraOpt.SelectedIndexes
            l.Add(Db.GetInt(_extras.Rows(i), "id"))
        Next
        Return l
    End Function

    Private Function ExtrasSum() As Decimal
        Dim t As Decimal = 0D
        For Each i As Integer In _extraOpt.SelectedIndexes
            t += Db.GetDec(_extras.Rows(i), "price")
        Next
        Return t
    End Function

    Private Function Dine() As String
        Return If(_dineOpt.SelectedIndex = 1, "take_out", "dine_in")
    End Function

    Private Sub Recalc()
        _price.Text = Theme.Peso(UnitPrice())
        _total.Text = Theme.Peso((UnitPrice() + ExtrasSum()) * _qty.Value)
        UpdateBookmark()
        _add.Enabled = _available AndAlso _stallOpen
        _add.Text = If(Not _available, "Unavailable", If(Not _stallOpen, "Stall closed", "Add to order"))
    End Sub

    Private Sub UpdateBookmark()
        Dim saved As Boolean = False
        Try
            saved = CartSvc.SavedId(Db.GetInt(_p, "id"), SizeId(), Dine(), ExtraIds()) > 0
        Catch
        End Try
        _bookmark.Filled = saved
        _bookmark.IconColor = If(saved, Theme.Primary, Theme.TextMuted)
        _bookmark.Redraw()
    End Sub

    Private Sub AddClicked(sender As Object, e As EventArgs)
        Try
            CartSvc.AddToOrder(Db.GetInt(_p, "id"), SizeId(), Dine(), _qty.Value, ExtraIds())
            _shell.OrderPanelCtl.Reload()
            _shell.Notify("Added to your order")
        Catch ex As Exception
            _shell.Notify("Could not add: " & ex.Message, True)
        End Try
    End Sub

    Private Sub BookmarkClicked(sender As Object, e As EventArgs)
        Try
            Dim pid As Integer = Db.GetInt(_p, "id")
            Dim id As Integer = CartSvc.SavedId(pid, SizeId(), Dine(), ExtraIds())
            If id > 0 Then
                CartSvc.SetSaved(id, False)
                _shell.Notify("Removed from your cart")
            Else
                CartSvc.SaveToCart(pid, SizeId(), Dine(), _qty.Value, ExtraIds())
                _shell.Notify("Saved to your cart")
            End If
            UpdateBookmark()
            _shell.OrderPanelCtl.Reload()
        Catch ex As Exception
            _shell.Notify("Could not save: " & ex.Message, True)
        End Try
    End Sub

    Protected Overrides Sub LayoutPage(contentWidth As Integer)
        If _p Is Nothing Then Return
        Dim y As Integer = 24
        _back.Location = New Point(M, y)
        y += 38

        Dim imgSize As Integer = Math.Max(240, Math.Min(400, CInt(contentWidth * 0.38)))
        _img.SetBounds(M, y, imgSize, imgSize)
        Dim colX As Integer = M + imgSize + 36
        Dim cw As Integer = Math.Max(340, contentWidth - imgSize - 36)

        Dim y2 As Integer = y
        _cat.Location = New Point(colX, y2)
        y2 += 24
        _title.Location = New Point(colX, y2)
        Ui.FitLabel(_title, cw)
        y2 += _title.Height + 6

        _avail.Location = New Point(colX, y2 + 2)
        Dim nx As Integer = colX + _avail.Width + 8
        If _closed.Visible Then
            _closed.Location = New Point(nx, y2 + 2)
            nx += _closed.Width + 8
        End If
        _stallLink.Location = New Point(nx + 4, y2 + 3)
        _rating.Location = New Point(nx + 4 + _stallLink.PreferredWidth + 14, y2 + 2)
        _rating.Visible = (nx + 4 + _stallLink.PreferredWidth + 14 + _rating.Width < colX + cw + 40)
        y2 += 38

        _desc.Location = New Point(colX, y2)
        Ui.FitLabel(_desc, cw)
        y2 += _desc.Height + 12
        _price.Location = New Point(colX, y2)
        y2 += 48

        If _sizes.Rows.Count > 0 Then
            _sizeCap.Location = New Point(colX, y2)
            y2 += 26
            _sizeOpt.SetBounds(colX, y2, cw, _sizeOpt.RequiredHeight(cw))
            y2 += _sizeOpt.Height + 16
        End If
        _dineCap.Location = New Point(colX, y2)
        y2 += 26
        _dineOpt.SetBounds(colX, y2, cw, _dineOpt.RequiredHeight(cw))
        y2 += _dineOpt.Height + 16
        If _extras.Rows.Count > 0 Then
            _extraCap.Location = New Point(colX, y2)
            y2 += 26
            _extraOpt.SetBounds(colX, y2, cw, _extraOpt.RequiredHeight(cw))
            y2 += _extraOpt.Height + 16
        End If

        Dim compact As Boolean = (cw < 600)
        Dim barH As Integer = If(compact, 148, 84)
        _bar.SetBounds(colX, y2, cw, barH)
        _qty.Location = New Point(18, 20)
        _totalCap.Location = New Point(168, 14)
        _total.Location = New Point(168, 34)
        If compact Then
            _bookmark.Location = New Point(cw - 18 - 52, 80)
            _add.SetBounds(18, 80, cw - 18 - 52 - 12 - 18, 52)
        Else
            _bookmark.Location = New Point(cw - 18 - 52, 16)
            _add.SetBounds(cw - 18 - 52 - 12 - 200, 16, 200, 52)
        End If
        y2 += barH + 28

        Dim ry As Integer = Math.Max(y + imgSize + 28, y2)
        _reviews.SetBounds(M, ry, contentWidth, 100)
        Dim rh As Integer = _reviews.LayoutFor(contentWidth)
        _reviews.SetBounds(M, ry, contentWidth, rh)

        SetContentHeight(ry + rh + 12)
    End Sub
End Class
