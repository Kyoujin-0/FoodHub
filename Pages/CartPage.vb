Imports System.Data
Imports System.Drawing
Imports System.Windows.Forms

''' <summary>Your cart: saved items grouped by stall. Items can be added to the current order from here.</summary>
Public Class CartPage
    Inherits PageBase
    Implements IRefreshable

    Private ReadOnly _shell As CustomerForm
    Private ReadOnly _title As Label
    Private ReadOnly _sub As Label
    Private ReadOnly _selAll As New RoundCheck()
    Private ReadOnly _selAllLbl As Label
    Private ReadOnly _addSel As RoundedButton
    Private ReadOnly _groups As New List(Of Control)
    Private ReadOnly _empty1 As Label
    Private ReadOnly _empty2 As Label
    Private ReadOnly _browse As RoundedButton
    Private ReadOnly _sel As New HashSet(Of Integer)
    Private _rows As DataTable

    Public Sub New(shell As CustomerForm)
        _shell = shell
        _title = Ui.Lbl("Your cart", 26, True)
        _sub = Ui.Lbl("", 11, False, Theme.TextMuted)
        _selAllLbl = Ui.Lbl("Select all", 10.5F, True)
        _selAllLbl.Cursor = Cursors.Hand
        AddHandler _selAllLbl.Click, Sub(s, e) _selAll.Checked = Not _selAll.Checked
        _addSel = Ui.PrimaryBtn("Add selected", 170, 46)
        _addSel.IconName = "plus"
        _empty1 = Ui.Lbl("Your cart is empty", 14, True)
        _empty2 = Ui.Lbl("Tap the bookmark on any product to keep it here for later.", 10.5F, False, Theme.TextMuted)
        _browse = Ui.PrimaryBtn("Browse food stalls", 200, 46)
        AddHandler _browse.Click, Sub(s, e) _shell.ShowHome()

        Controls.Add(_title)
        Controls.Add(_sub)
        Controls.Add(_selAll)
        Controls.Add(_selAllLbl)
        Controls.Add(_addSel)
        Controls.Add(_empty1)
        Controls.Add(_empty2)
        Controls.Add(_browse)

        AddHandler _selAll.CheckedChanged, AddressOf SelectAllChanged
        AddHandler _addSel.Click, AddressOf AddSelectedClicked

        LoadData()
    End Sub

    Private Sub LoadData()
        _rows = CartSvc.GetRows("saved")
        Dim ids As New HashSet(Of Integer)
        For Each r As DataRow In _rows.Rows
            ids.Add(Db.GetInt(r, "cart_id"))
        Next
        _sel.RemoveWhere(Function(i) Not ids.Contains(i))
        Dim n As Integer = _rows.Rows.Count
        _sub.Text = n.ToString() & If(n = 1, " saved item. ", " saved items. ") & "Favorites stay here after you order them."
        RelayoutPage()
    End Sub

    ''' <summary>Called when the Your order panel changed something.</summary>
    Public Sub RefreshData() Implements IRefreshable.RefreshData
        _rows = CartSvc.GetRows("saved")
        RelayoutKeepScroll()
    End Sub

    Private Sub ReloadAll()
        Dim pos As Integer = -AutoScrollPosition.Y
        _rows = CartSvc.GetRows("saved")
        Dim ids As New HashSet(Of Integer)
        For Each r As DataRow In _rows.Rows
            ids.Add(Db.GetInt(r, "cart_id"))
        Next
        _sel.RemoveWhere(Function(i) Not ids.Contains(i))
        Dim n As Integer = _rows.Rows.Count
        _sub.Text = n.ToString() & If(n = 1, " saved item. ", " saved items. ") & "Favorites stay here after you order them."
        RelayoutPage()
        If pos > 0 Then AutoScrollPosition = New Point(0, pos)
        _shell.OrderPanelCtl.Reload()
    End Sub

    Private _building As Boolean

    Private Sub SelectAllChanged(sender As Object, e As EventArgs)
        If _building Then Return
        _sel.Clear()
        If _selAll.Checked Then
            For Each r As DataRow In _rows.Rows
                _sel.Add(Db.GetInt(r, "cart_id"))
            Next
        End If
        RelayoutKeepScroll()
    End Sub

    Private Sub AddSelectedClicked(sender As Object, e As EventArgs)
        If _sel.Count = 0 Then
            _shell.Notify("Tick the items you want to add first.", True)
            Return
        End If
        Dim added As Integer = 0
        Try
            For Each r As DataRow In _rows.Rows
                Dim id As Integer = Db.GetInt(r, "cart_id")
                If Not _sel.Contains(id) Then Continue For
                If Db.GetBool(r, "in_order") OrElse Not Db.GetBool(r, "is_open") OrElse Not Db.GetBool(r, "is_available") Then Continue For
                CartSvc.SetInOrder(id, True)
                added += 1
            Next
        Catch ex As Exception
            _shell.Notify("Could not add: " & ex.Message, True)
            Return
        End Try
        If added = 0 Then
            _shell.Notify("Those items are already in your order or unavailable.", True)
            Return
        End If
        _sel.Clear()
        ReloadAll()
        _shell.Notify(added.ToString() & If(added = 1, " item", " items") & " added to your order")
    End Sub

    Protected Overrides Sub LayoutPage(contentWidth As Integer)
        If _rows Is Nothing Then Return
        Dim cw As Integer = Math.Min(contentWidth, 1100)
        _title.Location = New Point(M, 24)
        _sub.Location = New Point(M, 66)

        _addSel.Location = New Point(M + cw - _addSel.Width, 30)
        _selAllLbl.Location = New Point(_addSel.Left - 18 - _selAllLbl.PreferredWidth, 43)
        _selAll.Location = New Point(_selAllLbl.Left - 30, 42)

        For Each c As Control In _groups
            Controls.Remove(c)
            c.Dispose()
        Next
        _groups.Clear()

        _building = True
        _selAll.SetCheckedSilently(_rows.Rows.Count > 0 AndAlso _sel.Count = _rows.Rows.Count)
        _building = False

        Dim y As Integer = 118
        Dim hasRows As Boolean = _rows.Rows.Count > 0
        _selAll.Visible = hasRows
        _selAllLbl.Visible = hasRows
        _addSel.Visible = hasRows
        _empty1.Visible = Not hasRows
        _empty2.Visible = Not hasRows
        _browse.Visible = Not hasRows

        If Not hasRows Then
            _empty1.Location = New Point(M, y + 20)
            _empty2.Location = New Point(M, y + 54)
            _browse.Location = New Point(M, y + 92)
            SetContentHeight(y + 170)
            Return
        End If

        Dim group As New List(Of DataRow)
        Dim lastStall As Integer = -1
        For Each r As DataRow In _rows.Rows
            Dim sid As Integer = Db.GetInt(r, "stall_id")
            If sid <> lastStall AndAlso group.Count > 0 Then
                Dim g As RoundedPanel = BuildGroup(group, cw)
                g.Location = New Point(M, y)
                Controls.Add(g)
                _groups.Add(g)
                y += g.Height + 20
                group = New List(Of DataRow)
            End If
            lastStall = sid
            group.Add(r)
        Next
        If group.Count > 0 Then
            Dim g As RoundedPanel = BuildGroup(group, cw)
            g.Location = New Point(M, y)
            Controls.Add(g)
            _groups.Add(g)
            y += g.Height + 20
        End If
        SetContentHeight(y + 10)
    End Sub

    Private Function BuildGroup(items As List(Of DataRow), cw As Integer) As RoundedPanel
        Dim card As New RoundedPanel()
        card.Radius = 20
        card.FillColor = Color.White
        card.SurroundColor = Theme.Bg
        card.Shadow = True
        Dim first As DataRow = items(0)
        Dim stallOpen As Boolean = Db.GetBool(first, "is_open")

        ' stall header
        Dim stallChk As New RoundCheck()
        stallChk.Location = New Point(24, 22)
        Dim allSel As Boolean = True
        For Each r As DataRow In items
            If Not _sel.Contains(Db.GetInt(r, "cart_id")) Then allSel = False
        Next
        stallChk.SetCheckedSilently(allSel)
        Dim itemIds As New List(Of Integer)
        For Each r As DataRow In items
            itemIds.Add(Db.GetInt(r, "cart_id"))
        Next
        AddHandler stallChk.CheckedChanged, Sub(s, e)
                                                For Each id As Integer In itemIds
                                                    If stallChk.Checked Then _sel.Add(id) Else _sel.Remove(id)
                                                Next
                                                RelayoutKeepScroll()
                                            End Sub
        card.Controls.Add(stallChk)

        Dim logo As New CoverImage()
        logo.Circle = True
        logo.Image = Ui.LoadImage(Db.GetStr(first, "logo_path"))
        logo.PlaceholderText = Db.GetStr(first, "stall_name")
        logo.SetBounds(62, 12, 42, 42)
        card.Controls.Add(logo)
        Dim nm As Label = Ui.Lbl(Db.GetStr(first, "stall_name"), 12.5F, True)
        nm.Location = New Point(116, 20)
        card.Controls.Add(nm)
        Dim pill As PillLabel = Ui.OpenPill(stallOpen)
        pill.Location = New Point(116 + nm.PreferredWidth + 12, 21)
        card.Controls.Add(pill)

        Dim y As Integer = 66
        For Each r As DataRow In items
            Dim sep As New Panel()
            sep.BackColor = Theme.Border
            sep.SetBounds(24, y, cw - 48, 1)
            card.Controls.Add(sep)
            AddItemRow(card, r, y, cw, stallOpen)
            y += 120
        Next
        card.Size = New Size(cw, y + 8)
        Return card
    End Function

    Private Sub AddItemRow(card As RoundedPanel, r As DataRow, y As Integer, cw As Integer, stallOpen As Boolean)
        Dim cid As Integer = Db.GetInt(r, "cart_id")
        Dim inOrder As Boolean = Db.GetBool(r, "in_order")
        Dim available As Boolean = Db.GetBool(r, "is_available")
        Dim unit As Decimal = Db.GetDec(r, "unit_price") + Db.GetDec(r, "extras_total")

        Dim chk As New RoundCheck()
        chk.Location = New Point(24, y + 49)
        chk.SetCheckedSilently(_sel.Contains(cid))
        AddHandler chk.CheckedChanged, Sub(s, e)
                                           If chk.Checked Then _sel.Add(cid) Else _sel.Remove(cid)
                                           RelayoutKeepScroll()
                                       End Sub
        card.Controls.Add(chk)

        Dim img As New CoverImage()
        img.Radius = 16
        img.Image = Ui.LoadImage(Db.GetStr(r, "image_path"))
        img.PlaceholderText = Db.GetStr(r, "product_name")
        img.SetBounds(62, y + 20, 80, 80)
        card.Controls.Add(img)

        ' right-hand cluster, laid out from the right edge
        Dim rx As Integer = cw - 24
        Dim btn As RoundedButton
        If inOrder Then
            btn = Ui.SoftBtn("In your order", 132, 42)
            btn.Enabled = False
        ElseIf Not stallOpen Then
            btn = Ui.OutlineBtn("Stall closed", 132, 42)
            btn.Enabled = False
        ElseIf Not available Then
            btn = Ui.OutlineBtn("Unavailable", 132, 42)
            btn.Enabled = False
        Else
            btn = Ui.OutlineBtn("Add to order", 132, 42)
            AddHandler btn.Click, Sub(s, e)
                                      Try
                                          CartSvc.SetInOrder(cid, True)
                                          ReloadAll()
                                          _shell.Notify("Added to your order")
                                      Catch ex As Exception
                                          _shell.Notify("Could not add: " & ex.Message, True)
                                      End Try
                                  End Sub
        End If
        btn.Location = New Point(rx - 132, y + 39)
        card.Controls.Add(btn)

        Dim priceX As Integer = btn.Left - 14 - 104
        Dim total As New Label()
        total.AutoSize = False
        total.TextAlign = ContentAlignment.MiddleRight
        total.Font = Theme.F(12, True)
        total.Text = Theme.Peso(unit * Db.GetInt(r, "quantity"))
        total.SetBounds(priceX, y + 38, 104, 24)
        card.Controls.Add(total)
        Dim each1 As New Label()
        each1.AutoSize = False
        each1.TextAlign = ContentAlignment.MiddleRight
        each1.Font = Theme.F(9)
        each1.ForeColor = Theme.TextMuted
        each1.Text = Theme.Peso(unit) & " each"
        each1.SetBounds(priceX, y + 62, 104, 20)
        card.Controls.Add(each1)

        Dim trash As New IconButton()
        trash.IconName = "trash"
        trash.HoverIconColor = Theme.Danger
        trash.HoverBack = Theme.DangerSoft
        trash.Size = New Size(38, 38)
        trash.Location = New Point(priceX - 14 - 38, y + 41)
        AddHandler trash.Click, Sub(s, e)
                                    If Ui.Confirm("Remove this item from your cart?") Then
                                        CartSvc.DeleteItem(cid)
                                        _sel.Remove(cid)
                                        ReloadAll()
                                    End If
                                End Sub
        card.Controls.Add(trash)

        Dim heart As New IconButton()
        heart.IconName = "heart"
        heart.Filled = True
        heart.IconColor = Theme.Primary
        heart.Size = New Size(38, 38)
        heart.Location = New Point(trash.Left - 4 - 38, y + 41)
        AddHandler heart.Click, Sub(s, e)
                                    CartSvc.SetSaved(cid, False)
                                    _sel.Remove(cid)
                                    ReloadAll()
                                    _shell.Notify("Removed from favorites")
                                End Sub
        card.Controls.Add(heart)

        Dim q As New QtyStepper()
        q.Size = New Size(108, 36)
        q.Font = Theme.F(10.5F, True)
        q.SetValueSilently(Db.GetInt(r, "quantity"))
        q.Location = New Point(heart.Left - 16 - 108, y + 42)
        AddHandler q.ValueChanged, Sub(s, e)
                                       CartSvc.SetQty(cid, q.Value)
                                       total.Text = Theme.Peso(unit * q.Value)
                                       _shell.OrderPanelCtl.Reload()
                                   End Sub
        card.Controls.Add(q)

        Dim tx As Integer = 62 + 80 + 16
        Dim tw As Integer = Math.Max(120, q.Left - 16 - tx)
        Dim nm As Label = Ui.FixedLbl(Db.GetStr(r, "product_name"), 11.5F, tw, True)
        nm.Location = New Point(tx, y + 20)
        card.Controls.Add(nm)

        Dim parts As New List(Of String)
        If Db.GetStr(r, "size_name") <> "" Then parts.Add(Db.GetStr(r, "size_name"))
        If Db.GetStr(r, "extras_text") <> "" Then parts.Add("+ " & Db.GetStr(r, "extras_text"))
        If parts.Count = 0 Then parts.Add("Regular")
        Dim vl As Label = Ui.FixedLbl(String.Join("  " & ChrW(&HB7) & "  ", parts), 9.5F, tw, False, Theme.TextMuted)
        vl.Location = New Point(tx, y + 46)
        card.Controls.Add(vl)

        Dim take As Boolean = (Db.GetStr(r, "dine_option") = "take_out")
        Dim dp As New PillLabel()
        dp.Text = If(take, "Take out", "Dine in")
        dp.FillColor = If(take, Theme.WarningSoft, Theme.InfoSoft)
        dp.TextColor = If(take, Theme.Warning, Theme.Info)
        dp.FitToText()
        dp.Location = New Point(tx, y + 74)
        card.Controls.Add(dp)
    End Sub
End Class
