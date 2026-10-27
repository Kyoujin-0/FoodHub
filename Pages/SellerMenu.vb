Imports System.Data
Imports System.Drawing
Imports System.Windows.Forms
Imports Timer = System.Windows.Forms.Timer

''' <summary>Left side of the Menu tab: category chips, search and the grid of item cards (scrolls like the other pages).</summary>
Public Class MenuGridPage
    Inherits PageBase

    Public Event ItemSelected(productId As Integer)
    Public Event AddCategoryRequested()

    Private ReadOnly _stallId As Integer
    Private ReadOnly _chips As New MenuChipBar()
    Private ReadOnly _search As New AppTextBox()
    Private ReadOnly _empty As Label
    Private ReadOnly _cards As New List(Of MenuItemCard)
    Private ReadOnly _chipNames As New List(Of String)
    Private ReadOnly _debounce As New Timer()
    Private _items As DataTable
    Private _selectedId As Integer = -1

    Public Sub New(stallId As Integer)
        _stallId = stallId
        _search.LeftIcon = "search"
        _search.PlaceholderText = "Search items"
        _search.Font = Theme.F(11)
        _search.MaxLength = 40
        _empty = Ui.Lbl("", 11, False, Theme.TextMuted)
        Controls.Add(_chips)
        Controls.Add(_search)
        Controls.Add(_empty)

        _debounce.Interval = 250
        AddHandler _debounce.Tick, AddressOf OnDebounce
        AddHandler _search.TextChanged, AddressOf OnSearchChanged
        AddHandler _chips.SelectedChanged, AddressOf OnChipChanged
        AddHandler _chips.AddClicked, AddressOf OnAddChip
    End Sub

    ''' <summary>Name of the selected category chip, or "" for All.</summary>
    Public Function CurrentCategoryName() As String
        If _chips.SelectedIndex > 0 AndAlso _chips.SelectedIndex < _chipNames.Count Then Return _chipNames(_chips.SelectedIndex)
        Return ""
    End Function

    Public Function CategoryNames() As List(Of String)
        Dim l As New List(Of String)
        For i As Integer = 1 To _chipNames.Count - 1
            l.Add(_chipNames(i))
        Next
        Return l
    End Function

    ''' <summary>Re-read categories and items from MySQL.</summary>
    Public Sub Reload()
        Dim keep As String = CurrentCategoryName()
        Dim cats As DataTable = SellerSvc.GetMenuCategories(_stallId)
        _items = SellerSvc.GetMenuItems(_stallId)
        _chipNames.Clear()
        Dim names As New List(Of String)
        Dim counts As New List(Of Integer)
        _chipNames.Add("")
        names.Add("All")
        counts.Add(_items.Rows.Count)
        For Each r As DataRow In cats.Rows
            Dim id As Integer = Db.GetInt(r, "id")
            Dim n As Integer = 0
            For Each it As DataRow In _items.Rows
                If Db.GetInt(it, "category_id") = id Then n += 1
            Next
            _chipNames.Add(Db.GetStr(r, "name"))
            names.Add(Db.GetStr(r, "name"))
            counts.Add(n)
        Next
        Dim idx As Integer = _chipNames.IndexOf(keep)
        If idx < 0 Then idx = 0
        _chips.SetItems(names, counts, idx)
        BuildCards()
    End Sub

    Public Sub SetSelected(productId As Integer)
        _selectedId = productId
        For Each c As MenuItemCard In _cards
            c.Selected = (c.ProductId = productId)
        Next
    End Sub

    Private Sub BuildCards()
        For Each c As MenuItemCard In _cards
            Controls.Remove(c)
            c.Dispose()
        Next
        _cards.Clear()
        If _items Is Nothing Then Return
        Dim cat As String = CurrentCategoryName()
        Dim q As String = _search.Text.Trim()
        For Each r As DataRow In _items.Rows
            If cat <> "" AndAlso Db.GetStr(r, "category") <> cat Then Continue For
            If q <> "" AndAlso Db.GetStr(r, "name").IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 Then Continue For
            Dim id As Integer = Db.GetInt(r, "id")
            Dim card As New MenuItemCard(id, Db.GetStr(r, "name"), Db.GetStr(r, "category"), Db.GetDec(r, "from_price"), Db.GetInt(r, "size_count") > 0,
                                         Db.GetBool(r, "track_stock"), Db.GetInt(r, "stock"), Db.GetBool(r, "is_available"), Ui.LoadImage(Db.GetStr(r, "image_path")))
            card.Selected = (id = _selectedId)
            AddHandler card.CardClicked, AddressOf OnCardClicked
            AddHandler card.AvailabilityToggled, AddressOf OnToggle
            Controls.Add(card)
            _cards.Add(card)
        Next
        If _items.Rows.Count = 0 Then
            _empty.Text = "No items yet. Click " & ChrW(&H201C) & "+ Add item" & ChrW(&H201D) & " to create your first one."
        Else
            _empty.Text = "No items match."
        End If
        _empty.Visible = (_cards.Count = 0)
        RelayoutPage()
    End Sub

    Private Sub OnDebounce(sender As Object, e As EventArgs)
        _debounce.Stop()
        BuildCards()
    End Sub

    Private Sub OnSearchChanged(sender As Object, e As EventArgs)
        _debounce.Stop()
        _debounce.Start()
    End Sub

    Private Sub OnChipChanged(sender As Object, e As EventArgs)
        BuildCards()
    End Sub

    Private Sub OnAddChip(sender As Object, e As EventArgs)
        RaiseEvent AddCategoryRequested()
    End Sub

    Private Sub OnCardClicked(sender As Object, e As EventArgs)
        RaiseEvent ItemSelected(DirectCast(sender, MenuItemCard).ProductId)
    End Sub

    Private Sub OnToggle(sender As Object, e As EventArgs)
        Dim c As MenuItemCard = DirectCast(sender, MenuItemCard)
        Dim nv As Boolean = Not c.Available
        Try
            SellerSvc.SetAvailable(c.ProductId, _stallId, nv)
            c.Available = nv
            _items = SellerSvc.GetMenuItems(_stallId)
        Catch ex As Exception
            Toast.Show(Me, "Could not update the item: " & ex.Message, True)
        End Try
    End Sub

    Protected Overrides Sub LayoutPage(contentWidth As Integer)
        Dim y As Integer = 8
        Dim searchW As Integer = Math.Min(260, Math.Max(150, contentWidth \ 3))
        Dim chipsW As Integer = contentWidth - searchW - 16
        Dim chipsH As Integer = _chips.RequiredHeight(chipsW)
        _chips.SetBounds(M, y, chipsW, chipsH)
        _search.SetBounds(M + contentWidth - searchW, y, searchW, 48)
        y += Math.Max(chipsH, 48) + 22

        Dim gap As Integer = 18
        Dim cols As Integer = Math.Max(1, (contentWidth + gap) \ (200 + gap))
        Dim cardW As Integer = (contentWidth - gap * (cols - 1)) \ cols
        Dim cardH As Integer = MenuItemCard.HeightFor(cardW)
        _empty.Location = New Point(M, y + 8)
        For i As Integer = 0 To _cards.Count - 1
            Dim col As Integer = i Mod cols
            Dim row As Integer = i \ cols
            _cards(i).SetBounds(M + col * (cardW + gap), y + row * (cardH + gap), cardW, cardH)
        Next
        Dim rows As Integer = CInt(Math.Ceiling(_cards.Count / CDbl(cols)))
        SetContentHeight(y + If(_cards.Count = 0, 60, rows * (cardH + gap)) + 24)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            _debounce.Stop()
            _debounce.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class

''' <summary>The whole Menu tab: header buttons, the item grid and the add / edit panel.</summary>
Public Class SellerMenuView
    Inherits Panel

    Private ReadOnly _stallId As Integer
    Private ReadOnly _title As Label
    Private ReadOnly _add As RoundedButton
    Private ReadOnly _manage As RoundedButton
    Private ReadOnly _page As MenuGridPage
    Private ReadOnly _panel As ItemEditPanel

    Public Sub New(stallId As Integer, stallName As String)
        _stallId = stallId
        BackColor = Theme.Bg
        DoubleBuffered = True

        _title = Ui.Lbl("Menu", 26, True)
        _add = Ui.PrimaryBtn("+ Add item", 156, 52)
        _add.Radius = 26
        _add.Font = Theme.F(11.5F, True)
        _manage = Ui.OutlineBtn("Manage extras", 170, 52)
        _manage.Radius = 26
        _manage.FillColor = Color.White
        _manage.HoverColor = Theme.PrimarySoft
        _manage.TextColor = Theme.TextDark
        _manage.BorderColor = Theme.Border
        _manage.Font = Theme.F(11.5F, True)

        _page = New MenuGridPage(stallId)
        _panel = New ItemEditPanel(stallId)
        _panel.Visible = False

        Controls.Add(_title)
        Controls.Add(_add)
        Controls.Add(_manage)
        Controls.Add(_page)
        Controls.Add(_panel)

        AddHandler _add.Click, AddressOf OnAddItem
        AddHandler _manage.Click, AddressOf OnManageClick
        AddHandler _page.ItemSelected, AddressOf OnItemSelected
        AddHandler _page.AddCategoryRequested, AddressOf OnAddCategory
        AddHandler _panel.Saved, AddressOf OnPanelSaved
        AddHandler _panel.Deleted, AddressOf OnPanelDeleted
        AddHandler _panel.CloseRequested, AddressOf OnPanelClose
        AddHandler _panel.ManageExtrasRequested, AddressOf OnPanelManageExtras

        Try
            SellerSvc.EnsureExtrasLibrary(stallId)
            _panel.ReloadExtras()
            _page.Reload()
            _panel.SetCategories(_page.CategoryNames())
        Catch ex As Exception
            Ui.Warn("The menu needs a database update. Run patch_menu.sql once in MySQL, then restart the app." & vbCrLf & vbCrLf & ex.Message)
        End Try
    End Sub

    Private Sub DoLayout()
        If _page Is Nothing Then Return
        Dim w As Integer = ClientSize.Width
        Dim h As Integer = ClientSize.Height
        _title.Location = New Point(32, 22)
        _add.Location = New Point(w - 32 - _add.Width, 18)
        _manage.Location = New Point(_add.Left - 12 - _manage.Width, 18)
        Dim top As Integer = 96
        If _panel.Visible Then
            Dim leftW As Integer = Math.Max(500, w - ItemEditPanel.PanelW - 32 - 16)
            _page.SetBounds(0, top, leftW, Math.Max(100, h - top))
            _panel.SetBounds(w - 32 - ItemEditPanel.PanelW, top + 6, ItemEditPanel.PanelW, Math.Max(420, h - top - 22))
        Else
            _page.SetBounds(0, top, w, Math.Max(100, h - top))
        End If
    End Sub

    Protected Overrides Sub OnResize(eventargs As EventArgs)
        MyBase.OnResize(eventargs)
        DoLayout()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        Using b As New SolidBrush(Theme.Primary)
            e.Graphics.FillRectangle(b, 32, 10, 46, 5)
        End Using
    End Sub

    Private Sub ShowPanel()
        _panel.Visible = True
        DoLayout()
    End Sub

    Private Sub HidePanel()
        _panel.Visible = False
        _page.SetSelected(-1)
        DoLayout()
    End Sub

    Private Sub OnAddItem(sender As Object, e As EventArgs)
        _panel.SetCategories(_page.CategoryNames())
        _panel.StartNew(_page.CurrentCategoryName())
        _page.SetSelected(0)
        ShowPanel()
    End Sub

    Private Sub OnItemSelected(productId As Integer)
        _panel.SetCategories(_page.CategoryNames())
        _panel.LoadItem(productId)
        _page.SetSelected(productId)
        ShowPanel()
    End Sub

    Private Sub OnAddCategory()
        Using f As New PromptForm("New category", "Category name", "e.g. Milk Tea", 40)
            If f.ShowDialog(FindForm()) <> DialogResult.OK OrElse f.Value = "" Then Return
            Try
                SellerSvc.AddMenuCategory(_stallId, f.Value)
                _page.Reload()
                _panel.SetCategories(_page.CategoryNames())
                Toast.Show(Me, "Category added")
            Catch ex As Exception
                Toast.Show(Me, "Could not add the category: " & ex.Message, True)
            End Try
        End Using
    End Sub

    Private Sub OnManageClick(sender As Object, e As EventArgs)
        OpenExtrasManager()
    End Sub

    Private Sub OnPanelManageExtras()
        OpenExtrasManager()
    End Sub

    Private Sub OpenExtrasManager()
        Using f As New ExtrasManagerForm(_stallId)
            If f.ShowDialog(FindForm()) = DialogResult.OK Then
                _panel.ReloadExtras()
                Toast.Show(Me, "Extras saved")
            End If
        End Using
    End Sub

    Private Sub OnPanelSaved(productId As Integer)
        Try
            _page.Reload()
            _page.SetSelected(productId)
            _panel.SetCategories(_page.CategoryNames())
            _panel.LoadItem(productId)
            Toast.Show(Me, "Item saved")
        Catch ex As Exception
            Toast.Show(Me, "Saved, but the list could not refresh: " & ex.Message, True)
        End Try
    End Sub

    Private Sub OnPanelDeleted()
        HidePanel()
        Try
            _page.Reload()
            _panel.SetCategories(_page.CategoryNames())
        Catch
        End Try
        Toast.Show(Me, "Item deleted")
    End Sub

    Private Sub OnPanelClose()
        HidePanel()
    End Sub
End Class
