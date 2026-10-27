Imports System.Data
Imports System.Drawing
Imports System.Windows.Forms
Imports Timer = System.Windows.Forms.Timer

''' <summary>Receipt (paper style) or full order details (one card per stall with status and Message button).</summary>
Public Class OrderViewForm
    Inherits Form

    Private ReadOnly _orderId As Long
    Private ReadOnly _details As Boolean
    Private ReadOnly _scroll As New Panel()

    Public Sub New(orderId As Long, details As Boolean)
        _orderId = orderId
        _details = details
        Text = If(details, "Order details", "Receipt") & " #" & orderId.ToString()
        Font = Theme.F(9.75F)
        BackColor = Theme.Bg
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        ShowInTaskbar = False
        StartPosition = FormStartPosition.CenterParent
        _scroll.AutoScroll = True
        _scroll.BackColor = Theme.Bg
        Controls.Add(_scroll)
        BuildContent()
    End Sub

    Private Sub BuildContent()
        Dim order As DataRow = OrderSvc.GetOrder(_orderId)
        If order Is Nothing Then
            Me.Close()
            Return
        End If
        Dim stalls As DataTable = OrderSvc.GetOrderStalls(_orderId)
        Dim formW As Integer = If(_details, 640, 460)
        Dim pad As Integer = 24
        Dim cw As Integer = formW - pad * 2 - SystemInformation.VerticalScrollBarWidth
        Dim y As Integer = pad

        If Not _details Then
            Dim head As Label = Ui.Lbl(Theme.AppName, 18, True, Theme.Primary)
            head.Location = New Point(pad, y)
            _scroll.Controls.Add(head)
            Dim sub1 As Label = Ui.Lbl("Order receipt", 10.5F, False, Theme.TextMuted)
            sub1.Location = New Point(pad, y + 30)
            _scroll.Controls.Add(sub1)
            Dim num As Label = Ui.Lbl("#" & _orderId.ToString(), 18, True)
            num.Location = New Point(pad + cw - num.PreferredWidth, y)
            _scroll.Controls.Add(num)
            Dim dt As Label = Ui.Lbl(Db.GetDate(order, "created_at").ToString("MMM d, yyyy  h:mm tt"), 9.5F, False, Theme.TextMuted)
            dt.Location = New Point(pad + cw - dt.PreferredWidth, y + 30)
            _scroll.Controls.Add(dt)
            y += 66
        Else
            Dim num As Label = Ui.Lbl("Order #" & _orderId.ToString(), 20, True)
            num.Location = New Point(pad, y)
            _scroll.Controls.Add(num)
            Dim dt As Label = Ui.Lbl(Db.GetDate(order, "created_at").ToString("MMM d, yyyy  h:mm tt"), 10, False, Theme.TextMuted)
            dt.Location = New Point(pad, y + 36)
            _scroll.Controls.Add(dt)
            y += 74
        End If

        For Each s As DataRow In stalls.Rows
            Dim card As New RoundedPanel()
            card.Radius = 16
            card.FillColor = Color.White
            card.SurroundColor = Theme.Bg
            card.Shadow = True
            Dim iw As Integer = cw - 40
            Dim cy As Integer = 18

            Dim nm As Label = Ui.FixedLbl(Db.GetStr(s, "stall_name"), 12, iw - 150, True, Theme.TextDark)
            nm.Location = New Point(20, cy)
            card.Controls.Add(nm)
            Dim pill As PillLabel = Ui.StatusPill(Db.GetStr(s, "status"))
            pill.Location = New Point(20 + iw - pill.Width, cy)
            card.Controls.Add(pill)
            cy += 36

            Dim items As DataTable = OrderSvc.GetOrderItems(Db.GetInt(s, "id"))
            For Each it As DataRow In items.Rows
                Dim qty As Integer = Db.GetInt(it, "quantity")
                Dim line As Decimal = (Db.GetDec(it, "unit_price") + Db.GetDec(it, "extras_total")) * qty
                Dim name As Label = Ui.FixedLbl(qty.ToString() & " x  " & Db.GetStr(it, "product_name"), 10.5F, iw - 110, False, Theme.TextDark)
                name.Location = New Point(20, cy)
                card.Controls.Add(name)
                Dim pr As New Label()
                pr.AutoSize = False
                pr.TextAlign = ContentAlignment.MiddleRight
                pr.Font = Theme.F(10.5F, True)
                pr.Text = Theme.Peso(line)
                pr.SetBounds(20 + iw - 100, cy - 2, 100, 24)
                card.Controls.Add(pr)
                cy += 24
                Dim parts As New List(Of String)
                If Db.GetStr(it, "size_name") <> "" Then parts.Add(Db.GetStr(it, "size_name"))
                parts.Add(If(Db.GetStr(it, "dine_option") = "take_out", "Take out", "Dine in"))
                If Db.GetStr(it, "extras_text") <> "" Then parts.Add("+ " & Db.GetStr(it, "extras_text"))
                Dim vl As Label = Ui.FixedLbl(String.Join("  " & ChrW(&HB7) & "  ", parts), 9, iw - 110, False, Theme.TextMuted)
                vl.Location = New Point(34, cy)
                card.Controls.Add(vl)
                cy += 28
            Next

            Dim sep As New Panel()
            sep.BackColor = Theme.Border
            sep.SetBounds(20, cy, iw, 1)
            card.Controls.Add(sep)
            cy += 10
            Dim stl As Label = Ui.Lbl("Subtotal", 10, False, Theme.TextMuted)
            stl.Location = New Point(20, cy + 2)
            card.Controls.Add(stl)
            Dim sv As New Label()
            sv.AutoSize = False
            sv.TextAlign = ContentAlignment.MiddleRight
            sv.Font = Theme.F(12, True)
            sv.ForeColor = Theme.Primary
            sv.Text = Theme.Peso(Db.GetDec(s, "subtotal"))
            sv.SetBounds(20 + iw - 160, cy - 2, 160, 28)
            card.Controls.Add(sv)
            cy += 36

            If _details Then
                Dim stallName As String = Db.GetStr(s, "stall_name")
                Dim osId As Integer = Db.GetInt(s, "id")
                Dim msg As RoundedButton = Ui.OutlineBtn("Message stall", 160, 40)
                msg.IconName = "message"
                msg.Location = New Point(20, cy)
                AddHandler msg.Click, Sub(a, b)
                                          Using f As New MessageForm(osId, stallName, _orderId)
                                              f.ShowDialog(Me)
                                          End Using
                                      End Sub
                card.Controls.Add(msg)
                cy += 52
            End If

            card.SetBounds(pad, y, cw, cy + 6)
            _scroll.Controls.Add(card)
            y += card.Height + 16
        Next

        ' total
        Dim totalBox As New RoundedPanel()
        totalBox.Radius = 16
        totalBox.FillColor = Theme.PrimarySoft
        totalBox.SurroundColor = Theme.Bg
        totalBox.SetBounds(pad, y, cw, 64)
        Dim tl As Label = Ui.Lbl("Total", 12, True, Theme.PrimaryDark)
        tl.Location = New Point(20, 20)
        totalBox.Controls.Add(tl)
        Dim tv As New Label()
        tv.AutoSize = False
        tv.TextAlign = ContentAlignment.MiddleRight
        tv.Font = Theme.F(17, True)
        tv.ForeColor = Theme.PrimaryDark
        tv.Text = Theme.Peso(Db.GetDec(order, "total"))
        tv.SetBounds(cw - 20 - 200, 14, 200, 36)
        totalBox.Controls.Add(tv)
        _scroll.Controls.Add(totalBox)
        y += 64 + 14

        Dim note As Label = Ui.WrapLbl("Pay at the counter of each stall. There is no online payment.", 9.5F, cw, Theme.TextMuted)
        note.Location = New Point(pad, y)
        _scroll.Controls.Add(note)
        y += note.Height + 14

        Dim closeBtn As RoundedButton = Ui.PrimaryBtn("Close", cw, 46)
        closeBtn.Location = New Point(pad, y)
        AddHandler closeBtn.Click, Sub(a, b) Me.Close()
        _scroll.Controls.Add(closeBtn)
        y += 46 + pad

        Dim maxH As Integer = Math.Min(y, Screen.PrimaryScreen.WorkingArea.Height - 120)
        ClientSize = New Size(formW, Math.Max(360, maxH))
        _scroll.SetBounds(0, 0, ClientSize.Width, ClientSize.Height)
    End Sub
End Class


