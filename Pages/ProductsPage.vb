Imports System.Data
Imports System.Drawing
Imports System.Windows.Forms
Imports Timer = System.Windows.Forms.Timer

''' <summary>Products page. With a stall id it is the stall view (header + menu); with 0 it lists every product.</summary>
Public Class ProductsPage
    Inherits PageBase

    Private ReadOnly _shell As CustomerForm
    Private ReadOnly _stallId As Integer
    Private _stall As DataRow
    Private _back As Label
    Private ReadOnly _header As New RoundedPanel()
    Private _logo As CoverImage
    Private _title As Label
    Private _sub As Label
    Private _pill As PillLabel
    Private ReadOnly _search As New AppTextBox()
    Private ReadOnly _chips As New ChipBar()
    Private ReadOnly _cards As New List(Of ProductCard)
    Private ReadOnly _empty As Label
    Private ReadOnly _debounce As New Timer()

    Public Sub New(shell As CustomerForm, stallId As Integer)
        _shell = shell
        _stallId = stallId
        If stallId > 0 Then _stall = CatalogSvc.GetStall(stallId)

        _header.Radius = 20
        _header.FillColor = Color.White
        _header.SurroundColor = Theme.Bg
        _header.Shadow = True

        If _stall IsNot Nothing Then
            _back = Ui.Link(ChrW(&H2190) & "  Back to stalls", 10.5F)
            AddHandler _back.Click, Sub(s, e) _shell.ShowHome()
            Controls.Add(_back)

            _logo = New CoverImage()
            _logo.Circle = True
            _logo.Image = Ui.LoadImage(Db.GetStr(_stall, "logo_path"))
            _logo.PlaceholderText = Db.GetStr(_stall, "name")
            _logo.Size = New Size(76, 76)
            _header.Controls.Add(_logo)
            _title = Ui.Lbl(Db.GetStr(_stall, "name"), 20, True)
            _sub = Ui.Lbl(Db.GetStr(_stall, "category"), 10.5F, False, Theme.TextMuted)
            _pill = Ui.OpenPill(Db.GetBool(_stall, "is_open"))
            _header.Controls.Add(_pill)
        Else
            _title = Ui.Lbl("All products", 20, True)
            _sub = Ui.Lbl("Browse dishes from every food stall.", 10.5F, False, Theme.TextMuted)
        End If
        _header.Controls.Add(_title)
        _header.Controls.Add(_sub)

        _search.LeftIcon = "search"
        _search.PlaceholderText = "Search products"
        _search.MaxLength = 40
        _search.Size = New Size(320, 46)
        _header.Controls.Add(_search)

        _empty = Ui.Lbl("No products found.", 11, False, Theme.TextMuted)
        _empty.Visible = False

        Controls.Add(_header)
        Controls.Add(_chips)
        Controls.Add(_empty)

        Dim names As New List(Of String)
        names.Add("All")
        names.AddRange(CatalogSvc.GetMenuCategoryNames(_stallId))
        _chips.SetItems(names, 0)
        _chips.BackColor = Theme.Bg

        _debounce.Interval = 280
        AddHandler _debounce.Tick, Sub(s, e)
                                       _debounce.Stop()
                                       LoadProducts()
                                   End Sub
        AddHandler _search.TextChanged, Sub(s, e)
                                            _debounce.Stop()
                                            _debounce.Start()
                                        End Sub
        AddHandler _search.EnterPressed, Sub(s, e)
                                             _debounce.Stop()
                                             LoadProducts()
                                         End Sub
        AddHandler _chips.SelectedChanged, Sub(s, e) LoadProducts()

        LoadProducts()
    End Sub

    Private Sub LoadProducts()
        Dim cat As String = ""
        If _chips.SelectedIndex > 0 Then cat = _chips.SelectedText
        Dim dt As DataTable = CatalogSvc.GetProducts(_stallId, cat, _search.Text)
        For Each c As ProductCard In _cards
            Controls.Remove(c)
            c.Dispose()
        Next
        _cards.Clear()
        For Each r As DataRow In dt.Rows
            Dim sizes As Boolean = Db.GetInt(r, "size_count") > 0
            Dim opts As Boolean = sizes OrElse Db.GetInt(r, "extra_count") > 0
            Dim card As New ProductCard(Db.GetInt(r, "id"), Db.GetStr(r, "name"), Db.GetStr(r, "short_desc"), Db.GetDec(r, "from_price"), sizes, opts,
                                        Db.GetDbl(r, "avg_rating"), Db.GetInt(r, "review_count"), Db.GetStr(r, "stall_name"), (_stallId = 0),
                                        Db.GetBool(r, "is_available"), Db.GetBool(r, "is_open"), Ui.LoadImage(Db.GetStr(r, "image_path")))
            AddHandler card.Click, Sub(s, e) _shell.ShowProduct(DirectCast(s, ProductCard).ProductId, _stallId)
            AddHandler card.AddClicked, AddressOf OnAddClicked
            Controls.Add(card)
            _cards.Add(card)
        Next
        _empty.Visible = (dt.Rows.Count = 0)
        RelayoutPage()
    End Sub

    Private Sub OnAddClicked(sender As Object, e As EventArgs)
        Dim card As ProductCard = DirectCast(sender, ProductCard)
        If card.NeedsOptions Then
            _shell.ShowProduct(card.ProductId, _stallId)
            Return
        End If
        Try
            CartSvc.AddToOrder(card.ProductId, 0, "dine_in", 1, New List(Of Integer))
            _shell.OrderPanelCtl.Reload()
            _shell.Notify("Added to your order")
        Catch ex As Exception
            _shell.Notify("Could not add: " & ex.Message, True)
        End Try
    End Sub

    Protected Overrides Sub LayoutPage(contentWidth As Integer)
        Dim y As Integer = 24
        If _back IsNot Nothing Then
            _back.Location = New Point(M, y)
            y += 34
        End If
        Dim hh As Integer = If(_stall IsNot Nothing, 116, 96)
        _header.SetBounds(M, y, contentWidth, hh)
        If _stall IsNot Nothing Then
            _logo.Location = New Point(24, 20)
            _title.Location = New Point(120, 24)
            _sub.Location = New Point(120, 62)
            _pill.Location = New Point(120 + _sub.PreferredWidth + 14, 60)
        Else
            _title.Location = New Point(28, 20)
            _sub.Location = New Point(28, 54)
        End If
        Dim sw As Integer = Math.Min(340, Math.Max(220, contentWidth - 480))
        _search.SetBounds(contentWidth - 24 - sw, CInt((hh - 46) / 2), sw, 46)
        y += hh + 20

        _chips.SetBounds(M, y, contentWidth, 46)
        y += 46 + 18

        _empty.Location = New Point(M, y)
        Dim gap As Integer = 18
        Dim cols As Integer = Math.Max(1, CInt(Math.Floor((contentWidth + gap) / (260.0 + gap))))
        Dim cardW As Integer = CInt(Math.Floor((contentWidth - gap * (cols - 1)) / cols))
        Dim cardH As Integer = 398
        For i As Integer = 0 To _cards.Count - 1
            Dim col As Integer = i Mod cols
            Dim row As Integer = i \ cols
            _cards(i).SetBounds(M + col * (cardW + gap), y + row * (cardH + gap), cardW, cardH)
        Next
        Dim rows As Integer = CInt(Math.Ceiling(_cards.Count / CDbl(cols)))
        SetContentHeight(y + rows * (cardH + gap) + 20)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            _debounce.Stop()
            _debounce.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class
