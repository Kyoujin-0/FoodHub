Imports System.Data
Imports System.Drawing
Imports System.Windows.Forms
Imports Timer = System.Windows.Forms.Timer

''' <summary>Chat between the customer and one stall of an order: text and photos. Locked when the order is finished.
''' (Replaces the old MessageForm class that was at the bottom of Forms/OrderForms.vb.)</summary>
Public Class MessageForm
    Inherits Form

    Private ReadOnly _osId As Integer
    Private ReadOnly _chat As New ChatView()
    Private ReadOnly _composer As New Composer()
    Private ReadOnly _timer As New Timer()
    Private ReadOnly _stallName As String
    Private _lastId As Integer = -1
    Private _lastStatus As String = ""

    Public Sub New(orderStallId As Integer, stallName As String, orderId As Long)
        _osId = orderStallId
        _stallName = stallName
        Text = "Message " & stallName
        Font = Theme.F(9.75F)
        BackColor = Theme.Bg
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        ShowInTaskbar = False
        StartPosition = FormStartPosition.CenterParent
        ClientSize = New Size(480, 630)

        Dim title As Label = Ui.FixedLbl(stallName, 15, 430, True)
        title.Location = New Point(24, 18)
        Dim sub1 As Label = Ui.Lbl("About order #" & orderId.ToString(), 10, False, Theme.TextMuted)
        sub1.Location = New Point(24, 48)
        _chat.EmptyText = "No messages yet." & vbCrLf & "Ask the stall about your order here."
        _chat.SetBounds(0, 80, 480, 450)
        _composer.SetBounds(0, 530, 480, 84)

        Controls.Add(title)
        Controls.Add(sub1)
        Controls.Add(_chat)
        Controls.Add(_composer)

        AddHandler _composer.TextSent, AddressOf OnTextSent
        AddHandler _composer.ImagePicked, AddressOf OnImagePicked
        AddHandler _chat.ImageClicked, AddressOf OnImageClicked

        InboxSvc.PurgeOld()
        _timer.Interval = 5000
        AddHandler _timer.Tick, Sub(a, b) LoadMessages(False)
        _timer.Start()
        LoadMessages(True)
    End Sub

    Private Sub OnTextSent(body As String)
        Dim err As String = ""
        If Not InboxSvc.Send(_osId, body, "", err) Then
            _composer.Text = body
            Ui.Warn(err)
            LoadMessages(True)
            Return
        End If
        LoadMessages(True)
        _chat.ScrollToBottom()
    End Sub

    Private Sub OnImagePicked(filePath As String)
        If InboxUi.SendPickedImage(Me, _osId, filePath) Then
            LoadMessages(True)
            _chat.ScrollToBottom()
        Else
            LoadMessages(True)
        End If
    End Sub

    Private Sub OnImageClicked(storedPath As String)
        InboxUi.ViewPhoto(Me, storedPath)
    End Sub

    Private Sub LoadMessages(force As Boolean)
        Try
            Dim info As DataRow = InboxSvc.GetThreadInfo(_osId)
            If info Is Nothing Then Return
            Dim dt As DataTable = InboxSvc.GetThread(_osId)
            Dim lastId As Integer = If(dt.Rows.Count > 0, Db.GetInt(dt.Rows(dt.Rows.Count - 1), "id"), 0)
            Dim status As String = Db.GetStr(info, "status")
            If Not force AndAlso lastId = _lastId AndAlso status = _lastStatus Then Return
            _lastId = lastId
            _lastStatus = status
            _chat.SetMessages(dt, Session.UserId)
            _composer.LockedText = InboxUi.LockText(status)
            _composer.Locked = InboxUi.IsFinished(status)
            If dt.Rows.Count > 0 AndAlso Db.GetInt(dt.Rows(dt.Rows.Count - 1), "sender_id") <> Session.UserId Then
                InboxSvc.MarkRead(_osId)
            End If
        Catch
        End Try
    End Sub

    Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
        _timer.Stop()
        _timer.Dispose()
        MyBase.OnFormClosed(e)
    End Sub
End Class
