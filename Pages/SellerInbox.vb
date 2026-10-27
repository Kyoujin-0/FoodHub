Imports System.Data
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Text
Imports System.Windows.Forms
Imports Timer = System.Windows.Forms.Timer

''' <summary>Seller "Inbox" tab: conversations on the left (Active / Finished), chat on the right, Open orders popup to start a chat.</summary>
Public Class SellerInboxPage
    Inherits Panel

    Private ReadOnly _stallId As Integer
    Private ReadOnly _stallName As String
    Private ReadOnly _tabs As New FilterTabs()
    Private ReadOnly _list As New ConvoList()
    Private ReadOnly _header As New ChatHeader()
    Private ReadOnly _openBtn As RoundedButton
    Private ReadOnly _chat As New ChatView()
    Private ReadOnly _composer As New Composer()
    Private ReadOnly _poll As New Timer()
    Private ReadOnly _fTitle As Font = Theme.F(24, True)
    Private _card As Rectangle
    Private _leftW As Integer
    Private _active As New DataTable()
    Private _finished As New DataTable()
    Private _listSig As String = ""
    Private _listFailed As Boolean
    Private _openId As Integer
    Private _openStatus As String = ""
    Private _lastMsgId As Integer = -1

    Public Sub New(stallId As Integer, stallName As String)
        _stallId = stallId
        _stallName = stallName
        BackColor = Theme.Bg
        DoubleBuffered = True

        _openBtn = Ui.OutlineBtn("Open orders", 150, 46)

        Controls.Add(_header)
        Controls.Add(_chat)
        Controls.Add(_composer)
        Controls.Add(_list)
        Controls.Add(_tabs)
        Controls.Add(_openBtn)

        AddHandler _tabs.SelectedChanged, AddressOf OnTabChanged
        AddHandler _list.ConvoClicked, AddressOf OnConvoClicked
        AddHandler _openBtn.Click, AddressOf OnOpenOrders
        AddHandler _composer.TextSent, AddressOf OnTextSent
        AddHandler _composer.ImagePicked, AddressOf OnImagePicked
        AddHandler _chat.ImageClicked, AddressOf OnImageClicked

        InboxSvc.PurgeOld()
        ShowEmptyState()
        UpdateTabs(0)
        ReloadLists(True)

        _poll.Interval = 4000
        AddHandler _poll.Tick, AddressOf OnPoll
        _poll.Start()
    End Sub

    ' ---------------- layout / paint ----------------

    Private Sub DoLayoutAll()
        _card = New Rectangle(32, 8, Math.Max(600, Width - 64), Math.Max(300, Height - 8 - 16))
        _leftW = Math.Max(340, Math.Min(440, CInt(_card.Width * 0.27)))
        Dim rx As Integer = _card.X + _leftW + 1
        Dim rw As Integer = _card.Right - rx
        _tabs.SetBounds(_card.X + 24, _card.Y + 92, _leftW - 48, 44)
        _list.SetBounds(_card.X, _card.Y + 148, _leftW, _card.Height - 148 - 20)
        _header.SetBounds(rx, _card.Y + 16, rw, 80)
        _openBtn.SetBounds(_card.Right - 24 - 150, _card.Y + 16 + (80 - 46) \ 2, 150, 46)
        Dim compH As Integer = 84
        Dim compY As Integer = _card.Bottom - 12 - compH
        _composer.SetBounds(rx, compY, rw, compH)
        _chat.SetBounds(rx, _card.Y + 96, rw, Math.Max(60, compY - (_card.Y + 96)))
        _openBtn.BringToFront()
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        DoLayoutAll()
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        Dim g As Graphics = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias
        If _card.Width = 0 Then DoLayoutAll()
        Using sh As GraphicsPath = Theme.RoundRectPath(New Rectangle(_card.X, _card.Y + 3, _card.Width - 1, _card.Height), 28), sb As New SolidBrush(Color.FromArgb(18, 90, 50, 20))
            g.FillPath(sb, sh)
        End Using
        Using p As GraphicsPath = Theme.RoundRectPath(New Rectangle(_card.X, _card.Y, _card.Width - 1, _card.Height), 28)
            g.FillPath(Brushes.White, p)
            Dim st As GraphicsState = g.Save()
            g.SetClip(p)
            Using b As New SolidBrush(InboxUi.ChatBg)
                g.FillRectangle(b, _card.X + _leftW + 1, _card.Y + 96, _card.Width - _leftW - 1, _card.Height - 96)
            End Using
            Using pen As New Pen(Theme.Border, 1.0F)
                g.DrawLine(pen, _card.X + _leftW, _card.Y, _card.X + _leftW, _card.Bottom)
            End Using
            g.Restore(st)
        End Using
        Using b As New SolidBrush(Theme.Primary)
            g.FillRectangle(b, _card.X + 26, _card.Y + 26, 40, 5)
        End Using
        TextRenderer.DrawText(g, "Inbox", _fTitle, New Rectangle(_card.X + 24, _card.Y + 38, 240, 40), Theme.TextDark,
                              TextFormatFlags.NoPadding Or TextFormatFlags.VerticalCenter)
    End Sub

    ' ---------------- conversation list ----------------

    Private Shared Function Sig(dt As DataTable) As String
        Dim sb As New StringBuilder()
        For Each r As DataRow In dt.Rows
            sb.Append(Db.GetInt(r, "id")).Append(":").Append(Db.GetStr(r, "status")).Append(":").Append(Db.GetInt(r, "last_id")).Append(":").Append(Db.GetInt(r, "unread")).Append(";")
        Next
        Return sb.ToString()
    End Function

    Private Sub ReloadLists(force As Boolean)
        Try
            Dim a As DataTable = InboxSvc.GetConversations(_stallId, False)
            Dim f As DataTable = InboxSvc.GetConversations(_stallId, True)
            Dim s As String = Sig(a) & "|" & Sig(f)
            _listFailed = False
            If s = _listSig AndAlso Not force Then Return
            _listSig = s
            _active = a
            _finished = f
        Catch
            _listFailed = True
            _active = New DataTable()
            _finished = New DataTable()
            _listSig = ""
        End Try
        UpdateTabs(_tabs.SelectedIndex)
    End Sub

    Private Sub UpdateTabs(selected As Integer)
        Dim names As New List(Of String)
        names.Add("Active")
        names.Add("Finished")
        Dim counts As New List(Of Integer)
        counts.Add(_active.Rows.Count)
        counts.Add(_finished.Rows.Count)
        _tabs.SetItems(names, counts, selected)
        ApplyTab()
    End Sub

    Private Sub ApplyTab()
        Dim fin As Boolean = (_tabs.SelectedIndex = 1)
        _list.ShowExpiry = fin
        If _listFailed Then
            _list.EmptyText = "Messages could not be loaded. Run patch_inbox.sql on your database."
        ElseIf fin Then
            _list.EmptyText = "No finished conversations." & vbCrLf & "A chat stays here for " & InboxSvc.KeepDays.ToString() & " days after its order ends, then it is deleted."
        Else
            _list.EmptyText = "No active conversations." & vbCrLf & "Press Open orders to start one, or wait for a customer to write."
        End If
        _list.SetData(If(fin, _finished, _active))
        _list.SelectedId = _openId
    End Sub

    Private Sub OnTabChanged(sender As Object, e As EventArgs)
        ApplyTab()
    End Sub

    Private Sub OnConvoClicked(orderStallId As Integer)
        OpenConversation(orderStallId)
    End Sub

    ' ---------------- the open conversation ----------------

    Private Sub ShowEmptyState()
        _openId = 0
        _openStatus = ""
        _lastMsgId = -1
        _header.Setup("", 0, "")
        _chat.Reset()
        _chat.EmptyText = "Select a conversation on the left," & vbCrLf & "or press Open orders to start a new one."
        _composer.Visible = False
    End Sub

    Private Sub OpenConversation(orderStallId As Integer)
        If orderStallId = _openId Then Return
        _openId = orderStallId
        _lastMsgId = -1
        _openStatus = ""
        _chat.Reset()
        _composer.Visible = True
        LoadThread(True)
        If _openId = 0 Then Return
        Dim wantTab As Integer = If(InboxUi.IsFinished(_openStatus), 1, 0)
        If wantTab <> _tabs.SelectedIndex Then UpdateTabs(wantTab) Else _list.SelectedId = _openId
    End Sub

    Private Sub LoadThread(force As Boolean)
        If _openId = 0 Then Return
        Try
            Dim info As DataRow = InboxSvc.GetThreadInfo(_openId)
            If info Is Nothing Then
                ShowEmptyState()
                Return
            End If
            Dim dt As DataTable = InboxSvc.GetThread(_openId)
            Dim lastId As Integer = If(dt.Rows.Count > 0, Db.GetInt(dt.Rows(dt.Rows.Count - 1), "id"), 0)
            Dim status As String = Db.GetStr(info, "status")
            If Not force AndAlso lastId = _lastMsgId AndAlso status = _openStatus Then Return
            _lastMsgId = lastId
            _openStatus = status
            Dim name As String = Db.GetStr(info, "username")
            _header.Setup(name, Db.GetInt(info, "order_id"), status)
            _chat.EmptyText = "No messages yet." & vbCrLf & "Say hello to " & name & "."
            _chat.SetMessages(dt, Session.UserId)
            Dim fin As Boolean = InboxUi.IsFinished(status)
            _composer.LockedText = InboxUi.LockText(status)
            _composer.Locked = fin
            If dt.Rows.Count > 0 AndAlso Db.GetInt(dt.Rows(dt.Rows.Count - 1), "sender_id") <> Session.UserId Then
                InboxSvc.MarkRead(_openId)
                ReloadLists(False)
            End If
        Catch
        End Try
    End Sub

    Private Sub OnTextSent(body As String)
        If _openId = 0 Then Return
        Dim err As String = ""
        If Not InboxSvc.Send(_openId, body, "", err) Then
            _composer.Text = body
            Ui.Warn(err)
            LoadThread(True)
            Return
        End If
        LoadThread(True)
        _chat.ScrollToBottom()
        ReloadLists(False)
    End Sub

    Private Sub OnImagePicked(filePath As String)
        If _openId = 0 Then Return
        If InboxUi.SendPickedImage(Me, _openId, filePath) Then
            LoadThread(True)
            _chat.ScrollToBottom()
            ReloadLists(False)
        Else
            LoadThread(True)
        End If
    End Sub

    Private Sub OnImageClicked(storedPath As String)
        InboxUi.ViewPhoto(Me, storedPath)
    End Sub

    Private Sub OnOpenOrders(sender As Object, e As EventArgs)
        Dim pick As Integer = 0
        Using f As New OpenOrdersForm(_stallId)
            If f.ShowDialog(FindForm()) = DialogResult.OK Then pick = f.SelectedId
        End Using
        If pick > 0 Then
            If pick = _openId Then Return
            OpenConversation(pick)
        End If
    End Sub

    Private Sub OnPoll(sender As Object, e As EventArgs)
        Try
            ReloadLists(False)
            LoadThread(False)
        Catch
        End Try
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            _poll.Stop()
            _poll.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class

''' <summary>Popup with every running order of the stall; picking one opens (or starts) its chat. Result: SelectedId.</summary>
Public Class OpenOrdersForm
    Inherits Form

    Public SelectedId As Integer
    Private ReadOnly _scroll As New Panel()

    Public Sub New(stallId As Integer)
        Text = "Open orders"
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

        Dim formW As Integer = 560
        Dim pad As Integer = 24
        Dim cw As Integer = formW - pad * 2 - SystemInformation.VerticalScrollBarWidth
        Dim y As Integer = pad

        Dim title As Label = Ui.Lbl("Open orders", 20, True)
        title.Location = New Point(pad, y)
        _scroll.Controls.Add(title)
        Dim sub1 As Label = Ui.Lbl("Pick an order to message its customer.", 10.5F, False, Theme.TextMuted)
        sub1.Location = New Point(pad, y + 38)
        _scroll.Controls.Add(sub1)
        y += 78

        Dim dt As DataTable = Nothing
        Try
            dt = InboxSvc.GetOpenOrders(stallId)
        Catch ex As Exception
            Dim fail As Label = Ui.WrapLbl("Could not load the orders: " & ex.Message, 10.5F, cw, Theme.Danger)
            fail.Location = New Point(pad, y)
            _scroll.Controls.Add(fail)
            y += fail.Height + 16
        End Try

        If dt IsNot Nothing AndAlso dt.Rows.Count = 0 Then
            Dim none As Label = Ui.WrapLbl("No open orders right now. Orders that are placed, preparing or ready show up here.", 11, cw, Theme.TextMuted)
            none.Location = New Point(pad, y)
            _scroll.Controls.Add(none)
            y += none.Height + 16
        ElseIf dt IsNot Nothing Then
            For Each r As DataRow In dt.Rows
                Dim osId As Integer = Db.GetInt(r, "id")
                Dim card As New RoundedPanel()
                card.Radius = 16
                card.FillColor = Color.White
                card.SurroundColor = Theme.Bg
                card.Shadow = True
                Dim iw As Integer = cw - 40
                Dim cy As Integer = 18

                Dim nm As Label = Ui.FixedLbl(Db.GetStr(r, "username"), 12, iw - 150, True, Theme.TextDark)
                nm.Location = New Point(20, cy)
                card.Controls.Add(nm)
                Dim pill As PillLabel = Ui.StatusPill(Db.GetStr(r, "status"))
                pill.Location = New Point(20 + iw - pill.Width, cy)
                card.Controls.Add(pill)
                cy += 32

                Dim sub2 As Label = Ui.FixedLbl("Order #" & Db.GetInt(r, "order_id").ToString() & "  " & ChrW(&HB7) & "  " & Theme.Peso(Db.GetDec(r, "subtotal")) & "  " & ChrW(&HB7) & "  " & Db.GetDate(r, "created_at").ToString("MMM d, h:mm tt"), 9.5F, iw, False, Theme.TextMuted)
                sub2.Location = New Point(20, cy)
                card.Controls.Add(sub2)
                cy += 26

                Dim items As String = Db.GetStr(r, "items")
                If items.Length > 150 Then items = items.Substring(0, 147) & "..."
                If items <> "" Then
                    Dim il As Label = Ui.WrapLbl(items, 10, iw, Theme.TextDark)
                    il.Location = New Point(20, cy)
                    card.Controls.Add(il)
                    cy += il.Height + 10
                End If

                Dim hasChat As Boolean = Db.GetInt(r, "msg_count") > 0
                Dim btn As RoundedButton = If(hasChat, Ui.OutlineBtn("Open chat", 150, 42), Ui.PrimaryBtn("Start chat", 150, 42))
                btn.Location = New Point(20, cy)
                AddHandler btn.Click, Sub(a, b)
                                          SelectedId = osId
                                          DialogResult = DialogResult.OK
                                          Close()
                                      End Sub
                card.Controls.Add(btn)
                cy += 42 + 18

                card.SetBounds(pad, y, cw, cy)
                _scroll.Controls.Add(card)
                y += card.Height + 14
            Next
        End If

        y += pad - 14
        ClientSize = New Size(formW, Math.Max(300, Math.Min(y, Screen.PrimaryScreen.WorkingArea.Height - 160)))
        _scroll.SetBounds(0, 0, ClientSize.Width, ClientSize.Height)
    End Sub
End Class
