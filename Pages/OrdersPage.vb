Imports System.Data
Imports System.Drawing
Imports System.Windows.Forms
Imports Timer = System.Windows.Forms.Timer

''' <summary>My orders: Active / History tabs and one card per order.</summary>
Public Class OrdersPage
    Inherits PageBase

    Private ReadOnly _shell As CustomerForm
    Private ReadOnly _title As Label
    Private ReadOnly _sub As Label
    Private ReadOnly _tabs As New SegmentTabs()
    Private ReadOnly _cards As New List(Of Control)
    Private ReadOnly _empty1 As Label
    Private ReadOnly _empty2 As Label
    Private ReadOnly _browse As RoundedButton
    Private ReadOnly _stallCache As New Dictionary(Of Integer, DataTable)
    Private ReadOnly _timer As New Timer()
    Private _orders As DataTable
    Private _sig As String = ""

    Public Sub New(shell As CustomerForm)
        _shell = shell
        _title = Ui.Lbl("My orders", 26, True)
        _sub = Ui.Lbl("Track your orders from every stall.", 11, False, Theme.TextMuted)
        _empty1 = Ui.Lbl("", 14, True)
        _empty2 = Ui.Lbl("", 10.5F, False, Theme.TextMuted)
        _browse = Ui.PrimaryBtn("Browse food stalls", 200, 46)
        AddHandler _browse.Click, Sub(s, e) _shell.ShowHome()
        _tabs.Size = New Size(300, 48)
        _tabs.BackColor = Theme.Bg
        Controls.Add(_title)
        Controls.Add(_sub)
        Controls.Add(_tabs)
        Controls.Add(_empty1)
        Controls.Add(_empty2)
        Controls.Add(_browse)
        AddHandler _tabs.SelectedChanged, Sub(s, e) RelayoutPage()

        LoadData()
        _timer.Interval = 10000
        AddHandler _timer.Tick, Sub(s, e) PollForChanges()
        _timer.Start()
    End Sub

    Private Function Signature() As String
        Dim sb As New System.Text.StringBuilder()
        For Each r As DataRow In _orders.Rows
            sb.Append(Db.GetInt(r, "id")).Append(":")
            sb.Append(Db.GetInt(r, "open_count")).Append(":")
            Dim st As DataTable = OrderSvc.GetOrderStalls(Db.GetInt(r, "id"))
            For Each s As DataRow In st.Rows
                sb.Append(Db.GetStr(s, "status").Substring(0, 2))
            Next
            sb.Append(";")
        Next
        Return sb.ToString()
    End Function

    Private Sub LoadData()
        _orders = OrderSvc.GetOrders()
        _stallCache.Clear()
        Dim active As Integer = 0
        For Each r As DataRow In _orders.Rows
            _stallCache(Db.GetInt(r, "id")) = OrderSvc.GetOrderStalls(Db.GetInt(r, "id"))
            If Db.GetInt(r, "open_count") > 0 Then active += 1
        Next
        _tabs.SetItems(New String() {"Active", "History"}, New Integer() {active, 0})
        _sig = Signature()
        RelayoutPage()
    End Sub

    Private Sub PollForChanges()
        Try
            _orders = OrderSvc.GetOrders()
            If Signature() <> _sig Then LoadData()
        Catch
        End Try
    End Sub

    Protected Overrides Sub LayoutPage(contentWidth As Integer)
        Dim cw As Integer = Math.Min(contentWidth, 1000)
        _title.Location = New Point(M, 24)
        _sub.Location = New Point(M, 66)
        _tabs.Location = New Point(M, 104)
        Dim y As Integer = 176

        For Each c As Control In _cards
            Controls.Remove(c)
            c.Dispose()
        Next
        _cards.Clear()

        Dim wantActive As Boolean = (_tabs.SelectedIndex = 0)
        Dim shown As Integer = 0
        For Each r As DataRow In _orders.Rows
            Dim isActive As Boolean = Db.GetInt(r, "open_count") > 0
            If isActive <> wantActive Then Continue For
            Dim card As RoundedPanel = BuildCard(r, _stallCache(Db.GetInt(r, "id")), cw)
            card.Location = New Point(M, y)
            Controls.Add(card)
            _cards.Add(card)
            y += card.Height + 18
            shown += 1
        Next

        _empty1.Visible = (shown = 0)
        _empty2.Visible = (shown = 0)
        _browse.Visible = (shown = 0)
        If shown = 0 Then
            If wantActive Then
                _empty1.Text = "No active orders"
                _empty2.Text = "Orders you place will show up here while the stalls prepare them."
            Else
                _empty1.Text = "No past orders yet"
                _empty2.Text = "Completed orders will be kept here."
            End If
            _empty1.Location = New Point(M, y + 20)
            _empty2.Location = New Point(M, y + 54)
            _browse.Location = New Point(M, y + 92)
            _browse.Visible = wantActive
            y += 150
        End If
        SetContentHeight(y + 20)
    End Sub

    Private Function BuildCard(r As DataRow, stalls As DataTable, cw As Integer) As RoundedPanel
        Dim orderId As Integer = Db.GetInt(r, "id")
        Dim card As New RoundedPanel()
        card.Radius = 20
        card.FillColor = Color.White
        card.SurroundColor = Theme.Bg
        card.Shadow = True
        card.Cursor = Cursors.Hand

        Dim num As Label = Ui.Lbl("#" & orderId.ToString(), 18, True)
        num.Location = New Point(24, 18)
        card.Controls.Add(num)
        Dim dt As Label = Ui.Lbl(Db.GetDate(r, "created_at").ToString("MMM d, yyyy  " & ChrW(&HB7) & "  h:mm tt"), 10, False, Theme.TextMuted)
        dt.Location = New Point(24 + num.PreferredWidth + 14, 26)
        card.Controls.Add(dt)

        Dim receipt As Label = Ui.Link("View receipt", 10)
        receipt.Location = New Point(cw - 24 - receipt.PreferredWidth, 24)
        AddHandler receipt.Click, Sub(s, e)
                                      Using f As New OrderViewForm(orderId, False)
                                          f.ShowDialog(Me.FindForm())
                                      End Using
                                  End Sub
        card.Controls.Add(receipt)

        Dim n As Integer = Db.GetInt(r, "stall_count")
        Dim l1 As Label = Ui.Lbl(n.ToString() & If(n = 1, " stall", " stalls") & "  " & ChrW(&HB7) & "  ", 11, False, Theme.TextMuted)
        l1.Location = New Point(24, 64)
        card.Controls.Add(l1)
        Dim l2 As Label = Ui.Lbl(Theme.Peso(Db.GetDec(r, "total")), 13, True, Theme.Primary)
        l2.Location = New Point(24 + l1.PreferredWidth - 4, 61)
        card.Controls.Add(l2)

        Dim x As Integer = 24
        Dim y As Integer = 102
        Dim maxX As Integer = cw - 24
        For Each s As DataRow In stalls.Rows
            Dim pill As PillLabel = Ui.StatusPill(Db.GetStr(s, "status"), Db.GetStr(s, "stall_name") & "  " & ChrW(&HB7) & "  ")
            pill.Height = 28
            If x + pill.Width > maxX AndAlso x > 24 Then
                x = 24
                y += 36
            End If
            pill.Location = New Point(x, y)
            card.Controls.Add(pill)
            x += pill.Width + 10
        Next
        y += 28 + 18

        Dim msg As RoundedButton = Ui.OutlineBtn("Message stall", 160, 42)
        msg.IconName = "message"
        msg.Location = New Point(24, y)
        AddHandler msg.Click, Sub(s, e) MessageStall(orderId, stalls, msg)
        card.Controls.Add(msg)

        Dim arrow As New IconButton()
        arrow.IconName = "chev_r"
        arrow.Size = New Size(42, 42)
        arrow.BorderColor = Theme.Border
        arrow.Location = New Point(cw - 24 - 42, y)
        AddHandler arrow.Click, Sub(s, e) ShowDetails(orderId)
        card.Controls.Add(arrow)

        AddHandler card.Click, Sub(s, e) ShowDetails(orderId)
        AddHandler num.Click, Sub(s, e) ShowDetails(orderId)
        num.Cursor = Cursors.Hand

        card.Size = New Size(cw, y + 42 + 24)
        Return card
    End Function

    Private Sub ShowDetails(orderId As Integer)
        Using f As New OrderViewForm(orderId, True)
            f.ShowDialog(FindForm())
        End Using
    End Sub

    Private Sub MessageStall(orderId As Integer, stalls As DataTable, anchor As Control)
        If stalls.Rows.Count = 0 Then Return
        If stalls.Rows.Count = 1 Then
            OpenChat(Db.GetInt(stalls.Rows(0), "id"), Db.GetStr(stalls.Rows(0), "stall_name"), orderId)
            Return
        End If
        Dim cms As New ContextMenuStrip()
        cms.Font = Theme.F(10)
        For Each s As DataRow In stalls.Rows
            Dim osId As Integer = Db.GetInt(s, "id")
            Dim nm As String = Db.GetStr(s, "stall_name")
            Dim it As ToolStripItem = cms.Items.Add("Message " & nm)
            AddHandler it.Click, Sub(a, b) OpenChat(osId, nm, orderId)
        Next
        cms.Show(anchor, New Point(0, anchor.Height + 4))
    End Sub

    Private Sub OpenChat(orderStallId As Integer, stallName As String, orderId As Integer)
        Using f As New MessageForm(orderStallId, stallName, orderId)
            f.ShowDialog(FindForm())
        End Using
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            _timer.Stop()
            _timer.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class
