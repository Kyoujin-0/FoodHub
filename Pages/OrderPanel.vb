Imports System.Data
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

''' <summary>The "Your order" side panel shown on the Products, Product details and Cart pages.</summary>
Public Class OrderPanel
    Inherits Panel

    Public Const PanelWidth As Integer = 380
    Private Const FooterH As Integer = 292
    Private Const HeaderH As Integer = 68

    Public Event DataChanged As EventHandler
    Public Event OrderPlaced As EventHandler(Of Long)

    Private ReadOnly _title As Label
    Private ReadOnly _badge As PillLabel
    Private ReadOnly _list As New Panel()
    Private ReadOnly _footer As New Panel()
    Private ReadOnly _lblItems As Label
    Private ReadOnly _lblExtras As Label
    Private ReadOnly _lblTotal As Label
    Private ReadOnly _btnPlace As RoundedButton
    Private ReadOnly _note As RoundedPanel

    Public Sub New()
        SetStyle(ControlStyles.ResizeRedraw, True)
        BackColor = Color.White
        Width = PanelWidth
        _title = Ui.Lbl("Your order", 16, True)
        _title.Location = New Point(24, 22)
        _badge = New PillLabel()
        _badge.Text = "0"
        _badge.FillColor = Theme.Primary
        _badge.TextColor = Color.White
        _badge.Size = New Size(28, 28)
        _badge.Location = New Point(Width - 24 - 28, 22)

        _list.AutoScroll = True
        _list.BackColor = Color.White

        _footer.BackColor = Color.White

        _note = New RoundedPanel()
        _note.Radius = 14
        _note.FillColor = Theme.PrimarySoft
        _note.SurroundColor = Color.White
        _note.SetBounds(24, 4, Width - 48, 86)
        Dim nt As Label = Ui.Lbl("Pay at the counter", 10, True, Theme.PrimaryDark)
        nt.Location = New Point(16, 12)
        Dim nb As Label = Ui.WrapLbl("Pay each stall at its counter when you pick up your order. There is no online payment.", 9, _note.Width - 32, Color.FromArgb(120, 70, 40))
        nb.Location = New Point(16, 34)
        _note.Controls.Add(nt)
        _note.Controls.Add(nb)
        _footer.Controls.Add(_note)

        _lblItems = AddSummaryRow("Items total", 106, False)
        _lblExtras = AddSummaryRow("Extras total", 134, False)
        Dim line As New Panel()
        line.BackColor = Theme.Border
        line.SetBounds(24, 168, Width - 48, 1)
        _footer.Controls.Add(line)
        _lblTotal = AddSummaryRow("Total payment", 180, True)

        _btnPlace = Ui.PrimaryBtn("Place order", Width - 48, 54)
        _btnPlace.Radius = 16
        _btnPlace.Font = Theme.F(11, True)
        _btnPlace.Location = New Point(24, 224)
        AddHandler _btnPlace.Click, AddressOf PlaceClicked
        _footer.Controls.Add(_btnPlace)

        Controls.Add(_list)
        Controls.Add(_footer)
        Controls.Add(_title)
        Controls.Add(_badge)
        AddHandler Resize, Sub(s, e) DoLayoutPanel()
        AddHandler Paint, Sub(s, e)
                              Using p As New Pen(Theme.Border, 1)
                                  e.Graphics.DrawLine(p, 0, 0, 0, Height)
                              End Using
                          End Sub
        DoLayoutPanel()
    End Sub

    Private Function AddSummaryRow(caption As String, y As Integer, big As Boolean) As Label
        Dim c As Label = Ui.Lbl(caption, If(big, 12, 10), big, If(big, Theme.TextDark, Theme.TextMuted))
        c.Location = New Point(24, y + If(big, 2, 0))
        _footer.Controls.Add(c)
        Dim v As New Label()
        v.AutoSize = False
        v.TextAlign = ContentAlignment.MiddleRight
        v.Font = Theme.F(If(big, 15, 10.5F), True)
        v.ForeColor = If(big, Theme.Primary, Theme.TextDark)
        v.SetBounds(Width - 24 - 160, y - If(big, 3, 0), 160, If(big, 30, 22))
        v.Text = Theme.Peso(0)
        _footer.Controls.Add(v)
        Return v
    End Function

    Private Sub DoLayoutPanel()
        _badge.Location = New Point(Width - 24 - 28, 22)
        _footer.SetBounds(0, Height - FooterH, Width, FooterH)
        _list.SetBounds(0, HeaderH, Width, Math.Max(40, Height - FooterH - HeaderH))
    End Sub

    ''' <summary>Re-read the order from the database and rebuild the list.</summary>
    Public Sub Reload()
        If Session.UserId = 0 Then Return
        Dim dt As DataTable = CartSvc.GetRows("order")
        _list.SuspendLayout()
        Ui.DisposeChildren(_list)
        _list.AutoScrollPosition = New Point(0, 0)
        Dim w As Integer = Width - 48 - SystemInformation.VerticalScrollBarWidth
        Dim y As Integer = 4
        Dim totalQty As Integer = 0

        If dt.Rows.Count = 0 Then
            Dim t1 As Label = Ui.Lbl("Your order is empty", 12, True, Theme.TextDark)
            t1.Location = New Point(24, 70)
            Dim t2 As Label = Ui.WrapLbl("Add dishes from any stall and they will show up here.", 10, w, Theme.TextMuted)
            t2.Location = New Point(24, 100)
            _list.Controls.Add(t1)
            _list.Controls.Add(t2)
        Else
            Dim lastStall As Integer = -1
            For Each r As DataRow In dt.Rows
                totalQty += Db.GetInt(r, "quantity")
                Dim sid As Integer = Db.GetInt(r, "stall_id")
                If sid <> lastStall Then
                    lastStall = sid
                    y += 6
                    Dim sh As Label = Ui.FixedLbl(Db.GetStr(r, "stall_name"), 10, w - 70, True, Theme.TextDark)
                    sh.Location = New Point(24, y)
                    _list.Controls.Add(sh)
                    If Not Db.GetBool(r, "is_open") Then
                        Dim cp As PillLabel = Ui.OpenPill(False)
                        cp.Location = New Point(24 + w - cp.Width, y - 1)
                        _list.Controls.Add(cp)
                    End If
                    y += 28
                End If
                AddRow(r, y, w)
                y += 104
            Next
        End If
        Dim spacer As New Panel()
        spacer.SetBounds(0, y, 1, 10)
        _list.Controls.Add(spacer)
        _list.ResumeLayout()

        _badge.Text = totalQty.ToString()
        _badge.Visible = (totalQty > 0)
        RecalcTotals(dt)
    End Sub

    Private Sub AddRow(r As DataRow, y As Integer, w As Integer)
        Dim cid As Integer = Db.GetInt(r, "cart_id")
        Dim chk As New RoundCheck()
        chk.Location = New Point(24, y + 16)
        chk.SetCheckedSilently(Db.GetBool(r, "is_checked"))
        AddHandler chk.CheckedChanged, Sub(s, e)
                                           CartSvc.SetChecked(cid, chk.Checked)
                                           RecalcTotals(Nothing)
                                       End Sub
        _list.Controls.Add(chk)

        Dim img As New CoverImage()
        img.Radius = 12
        img.Image = Ui.LoadImage(Db.GetStr(r, "image_path"))
        img.PlaceholderText = Db.GetStr(r, "product_name")
        img.SetBounds(24 + 32, y + 8, 56, 56)
        _list.Controls.Add(img)

        Dim tx As Integer = 24 + 32 + 56 + 12
        Dim tw As Integer = w - (tx - 24) - 28
        Dim nm As Label = Ui.FixedLbl(Db.GetStr(r, "product_name"), 10, tw, True, Theme.TextDark)
        nm.Location = New Point(tx, y + 6)
        _list.Controls.Add(nm)

        Dim parts As New List(Of String)
        If Db.GetStr(r, "size_name") <> "" Then parts.Add(Db.GetStr(r, "size_name"))
        parts.Add(If(Db.GetStr(r, "dine_option") = "take_out", "Take out", "Dine in"))
        If Db.GetStr(r, "extras_text") <> "" Then parts.Add("+ " & Db.GetStr(r, "extras_text"))
        Dim varText As String = String.Join(" " & ChrW(&HB7) & " ", parts)
        Dim vc As Color = Theme.TextMuted
        If Not Db.GetBool(r, "is_available") Then
            varText = "Currently unavailable"
            vc = Theme.Danger
        End If
        Dim vl As Label = Ui.FixedLbl(varText, 8.5F, tw, False, vc)
        vl.Location = New Point(tx, y + 28)
        _list.Controls.Add(vl)

        Dim rm As New IconButton()
        rm.IconName = "x"
        rm.Size = New Size(26, 26)
        rm.Location = New Point(24 + w - 26, y + 4)
        AddHandler rm.Click, Sub(s, e)
                                 CartSvc.SetInOrder(cid, False)
                                 Reload()
                                 RaiseEvent DataChanged(Me, EventArgs.Empty)
                             End Sub
        _list.Controls.Add(rm)

        Dim q As New QtyStepper()
        q.Size = New Size(98, 30)
        q.Font = Theme.F(9.5F, True)
        q.Location = New Point(tx, y + 56)
        q.SetValueSilently(Db.GetInt(r, "quantity"))
        AddHandler q.ValueChanged, Sub(s, e)
                                       CartSvc.SetQty(cid, q.Value)
                                       Reload()
                                       RaiseEvent DataChanged(Me, EventArgs.Empty)
                                   End Sub
        _list.Controls.Add(q)

        Dim price As New Label()
        price.AutoSize = False
        price.TextAlign = ContentAlignment.MiddleRight
        price.Font = Theme.F(10.5F, True)
        price.ForeColor = Theme.TextDark
        price.Text = Theme.Peso(CartSvc.LineTotal(r))
        price.SetBounds(24 + w - 100, y + 56, 100, 30)
        _list.Controls.Add(price)

        Dim sep As New Panel()
        sep.BackColor = Theme.Border
        sep.SetBounds(24, y + 98, w, 1)
        _list.Controls.Add(sep)
    End Sub

    Private Sub RecalcTotals(dt As DataTable)
        If dt Is Nothing Then dt = CartSvc.GetRows("order")
        Dim items As Decimal = 0D
        Dim extras As Decimal = 0D
        Dim picked As Integer = 0
        For Each r As DataRow In dt.Rows
            If Not Db.GetBool(r, "is_checked") Then Continue For
            picked += 1
            items += Db.GetDec(r, "unit_price") * Db.GetInt(r, "quantity")
            extras += Db.GetDec(r, "extras_total") * Db.GetInt(r, "quantity")
        Next
        _lblItems.Text = Theme.Peso(items)
        _lblExtras.Text = Theme.Peso(extras)
        _lblTotal.Text = Theme.Peso(items + extras)
        If picked > 0 Then
            _btnPlace.Text = "Place order  " & ChrW(&HB7) & "  " & Theme.Peso(items + extras)
            _btnPlace.Enabled = True
        Else
            _btnPlace.Text = "Place order"
            _btnPlace.Enabled = False
        End If
    End Sub

    Private Sub PlaceClicked(sender As Object, e As EventArgs)
        Dim orderId As Long = 0
        Dim errMsg As String = ""
        Cursor = Cursors.WaitCursor
        Dim ok As Boolean = OrderSvc.PlaceOrder(orderId, errMsg)
        Cursor = Cursors.Default
        If Not ok Then
            Ui.Warn(errMsg)
            Return
        End If
        Reload()
        RaiseEvent OrderPlaced(Me, orderId)
    End Sub
End Class
