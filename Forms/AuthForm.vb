Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

''' <summary>Orange brand panel on the left of the login screens.</summary>
Public Class BrandPanel
    Inherits Panel

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        If Width < 20 OrElse Height < 20 Then Return
        g.SmoothingMode = SmoothingMode.AntiAlias
        Using lg As New LinearGradientBrush(New Rectangle(0, 0, Width, Height), ColorTranslator.FromHtml("#FF8F52"), ColorTranslator.FromHtml("#EE4F1C"), 60.0F)
            g.FillRectangle(lg, 0, 0, Width, Height)
        End Using
        Using br As New SolidBrush(Color.FromArgb(34, 255, 255, 255))
            g.FillEllipse(br, Width - 260, -120, 420, 420)
            g.FillEllipse(br, -140, Height - 260, 380, 380)
        End Using
        Using br As New SolidBrush(Color.FromArgb(22, 255, 255, 255))
            g.FillEllipse(br, Width - 150, Height - 330, 240, 240)
        End Using

        ' logo + name
        Using tile As GraphicsPath = Theme.RoundRectPath(New Rectangle(56, 56, 60, 60), 20), wb As New SolidBrush(Color.White)
            g.FillPath(wb, tile)
        End Using
        Icons.Draw(g, "logo", New Rectangle(68, 68, 36, 36), Theme.Primary)
        TextRenderer.DrawText(g, Theme.AppName, Theme.F(22, True), New Rectangle(130, 56, 300, 60), Color.White, TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)

        ' headline
        Dim top As Integer = CInt(Height * 0.33)
        TextRenderer.DrawText(g, "Skip the lines." & vbCrLf & "Order from every stall.", Theme.F(30, True), New Rectangle(56, top, Width - 112, 130), Color.White, TextFormatFlags.Left Or TextFormatFlags.NoPadding Or TextFormatFlags.WordBreak)
        TextRenderer.DrawText(g, "Browse menus, build one order and pick it up at the counter, all in one place.", Theme.F(12), New Rectangle(56, top + 138, Width - 130, 60), Color.FromArgb(235, 255, 255, 255), TextFormatFlags.Left Or TextFormatFlags.NoPadding Or TextFormatFlags.WordBreak)

        Dim items As String() = {"Browse every food stall", "One order for many stalls", "Track your order status live"}
        Dim y As Integer = Height - 56 - items.Length * 34
        For Each s As String In items
            Using cb As New SolidBrush(Color.FromArgb(70, 255, 255, 255))
                g.FillEllipse(cb, 56, y, 24, 24)
            End Using
            Icons.Draw(g, "check", New Rectangle(61, y + 5, 14, 14), Color.White)
            TextRenderer.DrawText(g, s, Theme.F(11, True), New Rectangle(92, y, Width - 140, 24), Color.White, TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
            y += 34
        Next
    End Sub
End Class

''' <summary>Log in / Register / Apply as a seller (three views swapped inside one card).</summary>
Public Class AuthForm
    Inherits Form

    Private Const CardW As Integer = 460
    Private Const InnerW As Integer = 388
    Private Const Pad As Integer = 36

    Private ReadOnly _brand As New BrandPanel()
    Private ReadOnly _right As New Panel()
    Private ReadOnly _card As New RoundedPanel()
    Private ReadOnly _loginView As New Panel()
    Private ReadOnly _registerView As New Panel()
    Private ReadOnly _sellerView As New Panel()
    Private ReadOnly _pendingView As New Panel()

    ' login
    Private _lUser As ValidatedField
    Private _lPass As ValidatedField
    Private _remember As RoundCheck
    Private _lMsg As Label

    ' register
    Private _rUser As ValidatedField
    Private _rPass As ValidatedField
    Private _rConfirm As ValidatedField

    ' seller
    Private _sUser As ValidatedField
    Private _sPass As ValidatedField
    Private _sStall As ValidatedField
    Private _sContact As ValidatedField

    ' waiting for approval
    Private _pendUser As String = ""
    Private _pendPass As String = ""
    Private _pUser As Label
    Private _pStall As Label
    Private _pMsg As Label

    Public Sub New()
        Text = Theme.AppName
        Font = Theme.F(9.75F)
        BackColor = Theme.Bg
        StartPosition = FormStartPosition.CenterScreen
        ClientSize = New Size(1180, 760)
        MinimumSize = New Size(1000, 640)
        DoubleBuffered = True

        _right.BackColor = Theme.Bg
        _right.AutoScroll = True
        _card.Radius = 26
        _card.FillColor = Color.White
        _card.SurroundColor = Theme.Bg
        _card.Shadow = True

        BuildLogin()
        BuildRegister()
        BuildSeller()
        BuildPending()

        _right.Controls.Add(_card)
        Controls.Add(_right)
        Controls.Add(_brand)
        AddHandler Resize, AddressOf OnFormResize

        ShowView("login")
        OnFormResize(Nothing, EventArgs.Empty)

        Dim remembered As String = Session.LoadRemembered()
        If remembered <> "" Then
            _lUser.Box.Text = remembered
            _remember.SetCheckedSilently(True)
        End If
    End Sub

    Private Sub OnFormResize(sender As Object, e As EventArgs)
        Dim lw As Integer = Math.Max(400, CInt(ClientSize.Width * 0.42))
        _brand.SetBounds(0, 0, lw, ClientSize.Height)
        _right.SetBounds(lw, 0, ClientSize.Width - lw, ClientSize.Height)
        CenterCard()
    End Sub

    Private Sub CenterCard()
        Dim x As Integer = Math.Max(16, CInt((_right.ClientSize.Width - CardW) / 2))
        Dim y As Integer = Math.Max(24, CInt((_right.ClientSize.Height - _card.Height) / 2))
        _card.Location = New Point(x, y)
    End Sub

    Private Sub Place(parent As Control, c As Control, x As Integer, y As Integer)
        c.Location = New Point(x, y)
        parent.Controls.Add(c)
    End Sub

    Private Function NewField(caption As String, isPassword As Boolean, maxLen As Integer) As ValidatedField
        Dim f As New ValidatedField()
        f.Caption = caption
        f.IsPassword = isPassword
        f.Box.MaxLength = maxLen
        f.Width = InnerW
        Return f
    End Function

    Private Sub AddPasswordRules(f As ValidatedField)
        f.AddRule("6+ characters", Function(s) Validator.PasswordLengthOk(s))
        f.AddRule("1 capital letter", Function(s) Validator.PasswordHasCapital(s))
        f.AddRule("1 number", Function(s) Validator.PasswordHasNumber(s))
    End Sub

    Private Sub AddUsernameRules(f As ValidatedField)
        f.AddRule("4-20 characters", Function(s) s.Trim().Length >= 4 AndAlso s.Trim().Length <= 20)
        f.AddRule("Letters, numbers, _", Function(s) s.Length > 0 AndAlso System.Text.RegularExpressions.Regex.IsMatch(s, "^[A-Za-z0-9_]+$"))
        f.AddRule("Username available", Function(s) Validator.UsernameFormatOk(s) AndAlso Not AuthSvc.UsernameTaken(s))
    End Sub

    Private Function Heading(parent As Control, title As String, subtitle As String) As Integer
        Place(parent, Ui.Lbl(title, 22, True), 0, 0)
        Dim sub1 As Label = Ui.WrapLbl(subtitle, 10.5F, InnerW, Theme.TextMuted)
        Place(parent, sub1, 0, 40)
        Return 40 + sub1.Height + 16
    End Function

    Private Sub FooterLink(parent As Control, prefix As String, linkText As String, y As Integer, onClick As EventHandler)
        Dim l1 As Label = Ui.Lbl(prefix, 10, False, Theme.TextMuted)
        Place(parent, l1, 0, y)
        Dim l2 As Label = Ui.Link(linkText, 10)
        AddHandler l2.Click, onClick
        Dim totalW As Integer = l1.PreferredWidth + 4 + l2.PreferredWidth
        Dim startX As Integer = Math.Max(0, CInt((InnerW - totalW) / 2))
        l1.Left = startX
        Place(parent, l2, startX + l1.PreferredWidth + 2, y)
    End Sub

    ' ---------------- LOGIN ----------------
    Private Sub BuildLogin()
        _loginView.BackColor = Color.White
        Dim y As Integer = Heading(_loginView, "Welcome back", "Log in to order from your favorite food stalls.")

        _lUser = NewField("Username", False, 20)
        Place(_loginView, _lUser, 0, y)
        y += 76
        _lPass = NewField("Password", True, 64)
        Place(_loginView, _lPass, 0, y)
        y += 76

        _remember = New RoundCheck()
        Place(_loginView, _remember, 0, y)
        Dim rl As Label = Ui.Lbl("Remember me", 10, False, Theme.TextDark)
        rl.Cursor = Cursors.Hand
        AddHandler rl.Click, Sub(s, ev) _remember.Checked = Not _remember.Checked
        Place(_loginView, rl, 30, y + 1)
        y += 36

        _lMsg = New Label()
        _lMsg.AutoSize = False
        _lMsg.Font = Theme.F(9.5F, True)
        _lMsg.ForeColor = Theme.Danger
        _lMsg.SetBounds(0, y, InnerW, 22)
        _loginView.Controls.Add(_lMsg)
        y += 28

        Dim btn As RoundedButton = Ui.PrimaryBtn("Log in", InnerW, 50)
        AddHandler btn.Click, Sub(s, ev) DoLogin()
        Place(_loginView, btn, 0, y)
        y += 72

        Dim hint As Label = Ui.Lbl("New here?", 10, False, Theme.TextMuted)
        Place(_loginView, hint, CInt((InnerW - hint.PreferredWidth) / 2), y)
        y += 28
        Dim signUp As RoundedButton = Ui.OutlineBtn("Sign up", InnerW, 48)
        AddHandler signUp.Click, Sub(s, ev) ShowView("register")
        Place(_loginView, signUp, 0, y)
        y += 48

        _loginView.Size = New Size(InnerW, y)

        AddHandler _lUser.Box.EnterPressed, Sub(s, ev) _lPass.Box.FocusInput()
        AddHandler _lPass.Box.EnterPressed, Sub(s, ev) DoLogin()
    End Sub

    Private Sub SetLoginMsg(text As String, isError As Boolean)
        _lMsg.ForeColor = If(isError, Theme.Danger, Theme.Success)
        _lMsg.Text = text
    End Sub

    Private Sub DoLogin()
        Dim u As String = _lUser.Value.Trim()
        Dim p As String = _lPass.Value
        If u = "" OrElse p = "" Then
            SetLoginMsg("Enter your username and password.", True)
            Return
        End If
        Dim errMsg As String = ""
        Dim ok As Boolean = False
        Cursor = Cursors.WaitCursor
        Try
            ok = AuthSvc.Login(u, p, errMsg)
        Catch ex As Exception
            Cursor = Cursors.Default
            SetLoginMsg("Database error: " & ex.Message, True)
            Return
        End Try
        Cursor = Cursors.Default
        If Not ok Then
            If AuthSvc.LastLoginStatus = "pending" Then
                ShowPending(u, p)
                Return
            End If
            SetLoginMsg(errMsg, True)
            Return
        End If
        FinishLogin()
    End Sub

    Private Sub FinishLogin()
        Session.SaveRemembered(If(_remember.Checked, Session.Username, ""))
        DialogResult = DialogResult.OK
    End Sub

    ' ---------------- REGISTER ----------------
    Private Sub BuildRegister()
        _registerView.BackColor = Color.White
        Dim y As Integer = Heading(_registerView, "Create your account", "Join to order from every food stall in one place.")

        _rUser = NewField("Username", False, 20)
        AddUsernameRules(_rUser)
        Place(_registerView, _rUser, 0, y)
        y += 98
        _rPass = NewField("Password", True, 64)
        AddPasswordRules(_rPass)
        Place(_registerView, _rPass, 0, y)
        y += 98
        _rConfirm = NewField("Confirm password", True, 64)
        _rConfirm.AddRule("Passwords match", Function(s) s.Length > 0 AndAlso s = _rPass.Value)
        Place(_registerView, _rConfirm, 0, y)
        y += 100

        Dim create As RoundedButton = Ui.PrimaryBtn("Create account", InnerW, 50)
        AddHandler create.Click, Sub(s, ev) DoRegister()
        Place(_registerView, create, 0, y)
        y += 62
        Dim asSeller As RoundedButton = Ui.OutlineBtn("Register as a seller", InnerW, 48)
        AddHandler asSeller.Click, Sub(s, ev) ShowView("seller")
        Place(_registerView, asSeller, 0, y)
        y += 66
        FooterLink(_registerView, "Already have an account?", "Log in", y, Sub(s, ev) ShowView("login"))
        y += 24

        _registerView.Size = New Size(InnerW, y)

        AddHandler _rPass.ValueChanged, Sub(s, ev) _rConfirm.Evaluate()
        AddHandler _rUser.Box.EnterPressed, Sub(s, ev) _rPass.Box.FocusInput()
        AddHandler _rPass.Box.EnterPressed, Sub(s, ev) _rConfirm.Box.FocusInput()
        AddHandler _rConfirm.Box.EnterPressed, Sub(s, ev) DoRegister()
    End Sub

    Private Sub DoRegister()
        Dim a As Boolean = _rUser.IsValid()
        Dim b As Boolean = _rPass.IsValid()
        Dim c As Boolean = _rConfirm.IsValid()
        If Not (a AndAlso b AndAlso c) Then Return
        Dim errMsg As String = ""
        Dim name As String = _rUser.Value.Trim()
        Cursor = Cursors.WaitCursor
        Dim ok As Boolean = AuthSvc.Register(name, _rPass.Value, errMsg)
        Cursor = Cursors.Default
        If Not ok Then
            Ui.Warn(errMsg)
            Return
        End If
        _rUser.ClearValue()
        _rPass.ClearValue()
        _rConfirm.ClearValue()
        ShowView("login")
        _lUser.Box.Text = name
        _lPass.Box.Text = ""
        SetLoginMsg("Account created! Log in to continue.", False)
        _lPass.Box.FocusInput()
    End Sub

    ' ---------------- APPLY AS SELLER ----------------
    Private Sub BuildSeller()
        _sellerView.BackColor = Color.White
        Dim y As Integer = Heading(_sellerView, "Apply as a seller", "An admin reviews every request before your stall goes live.")

        _sUser = NewField("Username", False, 20)
        AddUsernameRules(_sUser)
        Place(_sellerView, _sUser, 0, y)
        y += 98
        _sPass = NewField("Password", True, 64)
        AddPasswordRules(_sPass)
        Place(_sellerView, _sPass, 0, y)
        y += 98
        _sStall = NewField("Stall name", False, 40)
        _sStall.AddRule("3-40 characters", Function(s) s.Trim().Length >= 3 AndAlso s.Trim().Length <= 40)
        _sStall.AddRule("Not already registered", Function(s) s.Trim().Length > 0 AndAlso Not AuthSvc.StallNameTaken(s.Trim()))
        Place(_sellerView, _sStall, 0, y)
        y += 98
        _sContact = NewField("Contact number", False, 13)
        _sContact.AddRule("Starts with 09 or +63", Function(s) Validator.PhoneStartOk(s))
        _sContact.AddRule("Valid mobile number", Function(s) Validator.PhoneOk(s))
        _sContact.Box.Text = "09"
        AddHandler _sContact.Box.Inner.KeyPress, AddressOf ContactKeyPress
        AddHandler _sContact.Box.Inner.Enter, Sub(s, ev) _sContact.Box.Inner.SelectionStart = _sContact.Box.Inner.Text.Length
        Place(_sellerView, _sContact, 0, y)
        y += 100

        Dim send As RoundedButton = Ui.PrimaryBtn("Send request", InnerW, 50)
        AddHandler send.Click, Sub(s, ev) DoSeller()
        Place(_sellerView, send, 0, y)
        y += 62
        Dim asCustomer As RoundedButton = Ui.OutlineBtn("Register as a customer", InnerW, 48)
        AddHandler asCustomer.Click, Sub(s, ev) ShowView("register")
        Place(_sellerView, asCustomer, 0, y)
        y += 66
        FooterLink(_sellerView, "Already have an account?", "Log in", y, Sub(s, ev) ShowView("login"))
        y += 24

        _sellerView.Size = New Size(InnerW, y)

        AddHandler _sUser.Box.EnterPressed, Sub(s, ev) _sPass.Box.FocusInput()
        AddHandler _sPass.Box.EnterPressed, Sub(s, ev) _sStall.Box.FocusInput()
        AddHandler _sStall.Box.EnterPressed, Sub(s, ev) _sContact.Box.FocusInput()
        AddHandler _sContact.Box.EnterPressed, Sub(s, ev) DoSeller()
    End Sub

    Private Sub ContactKeyPress(sender As Object, e As KeyPressEventArgs)
        If Char.IsControl(e.KeyChar) Then Return
        If Char.IsDigit(e.KeyChar) Then Return
        If e.KeyChar = "+"c Then Return
        e.Handled = True
    End Sub

    Private Sub DoSeller()
        Dim a As Boolean = _sUser.IsValid()
        Dim b As Boolean = _sPass.IsValid()
        Dim c As Boolean = _sStall.IsValid()
        Dim d As Boolean = _sContact.IsValid()
        If Not (a AndAlso b AndAlso c AndAlso d) Then Return
        Dim errMsg As String = ""
        Dim name As String = _sUser.Value.Trim()
        Cursor = Cursors.WaitCursor
        Dim ok As Boolean = AuthSvc.ApplySeller(name, _sPass.Value, _sStall.Value.Trim(), _sContact.Value.Trim(), errMsg)
        Cursor = Cursors.Default
        If Not ok Then
            Ui.Warn(errMsg)
            Return
        End If
        _sUser.ClearValue()
        _sPass.ClearValue()
        _sStall.ClearValue()
        _sContact.ClearValue()
        _sContact.Box.Text = "09"
        Ui.Info("Your request was sent! You can log in once an admin approves your stall.", "Request sent")
        ShowView("login")
        _lUser.Box.Text = name
        _lPass.Box.Text = ""
        SetLoginMsg("", False)
    End Sub

    ' ---------------- WAITING FOR APPROVAL ----------------
    Private Sub BuildPending()
        _pendingView.BackColor = Color.White
        Dim badge As New PendingBadge()
        badge.Size = New Size(84, 84)
        Place(_pendingView, badge, 0, 0)
        Dim y As Integer = 104
        Place(_pendingView, Ui.Lbl("Waiting for approval", 22, True), 0, y)
        y += 44
        Dim info As Label = Ui.WrapLbl("Your seller request was received. An admin has to approve your stall before you can log in. Please check back later.", 10.5F, InnerW, Theme.TextMuted)
        Place(_pendingView, info, 0, y)
        y += info.Height + 18

        _pUser = Ui.Lbl("Seller", 10.5F, True, Theme.TextDark)
        Place(_pendingView, _pUser, 0, y)
        y += 28
        _pStall = Ui.Lbl("Stall", 10.5F, True, Theme.TextDark)
        Place(_pendingView, _pStall, 0, y)
        y += 38

        _pMsg = New Label()
        _pMsg.AutoSize = False
        _pMsg.Font = Theme.F(9.5F, True)
        _pMsg.ForeColor = Theme.TextMuted
        _pMsg.SetBounds(0, y, InnerW, 22)
        _pendingView.Controls.Add(_pMsg)
        y += 30

        Dim again As RoundedButton = Ui.PrimaryBtn("Check again", InnerW, 50)
        AddHandler again.Click, Sub(s, ev) CheckPending()
        Place(_pendingView, again, 0, y)
        y += 62
        Dim back As RoundedButton = Ui.OutlineBtn("Back to login", InnerW, 48)
        AddHandler back.Click, Sub(s, ev) LeavePending()
        Place(_pendingView, back, 0, y)
        y += 48
        _pendingView.Size = New Size(InnerW, y)
    End Sub

    ''' <summary>A seller whose request is not approved yet logs in: show the waiting page (the password stays in memory only until they leave it).</summary>
    Private Sub ShowPending(username As String, password As String)
        _pendUser = username
        _pendPass = password
        _pUser.Text = "Seller: " & username
        Dim stall As String = AuthSvc.PendingStallName(username)
        _pStall.Text = If(stall = "", "Stall: not found", "Stall: " & stall)
        _pMsg.ForeColor = Theme.TextMuted
        _pMsg.Text = ""
        ShowView("pending")
    End Sub

    Private Sub CheckPending()
        Dim errMsg As String = ""
        Dim ok As Boolean = False
        Cursor = Cursors.WaitCursor
        Try
            ok = AuthSvc.Login(_pendUser, _pendPass, errMsg)
        Catch ex As Exception
            Cursor = Cursors.Default
            _pMsg.ForeColor = Theme.Danger
            _pMsg.Text = "Database error: " & ex.Message
            Return
        End Try
        Cursor = Cursors.Default
        If ok Then
            _pendPass = ""
            FinishLogin()
            Return
        End If
        If AuthSvc.LastLoginStatus = "pending" Then
            _pMsg.ForeColor = Theme.TextMuted
            _pMsg.Text = "Still waiting for an admin. Try again later."
            Return
        End If
        ' rejected, deactivated or the password was changed
        LeavePending()
        SetLoginMsg(errMsg, True)
    End Sub

    Private Sub LeavePending()
        _pendUser = ""
        _pendPass = ""
        _lPass.Box.Text = ""
        ShowView("login")
    End Sub

    ' ---------------- view switching ----------------
    Private Sub ShowView(name As String)
        _card.SuspendLayout()
        Dim view As Panel = _loginView
        If name = "register" Then view = _registerView
        If name = "seller" Then view = _sellerView
        If name = "pending" Then view = _pendingView
        _card.Controls.Clear()
        view.Location = New Point(Pad, Pad)
        _card.Controls.Add(view)
        _card.Size = New Size(CardW, view.Height + Pad * 2)
        _card.ResumeLayout()
        If name = "login" Then _lMsg.Text = If(_lMsg.ForeColor = Theme.Success, _lMsg.Text, "")
        CenterCard()
        _right.AutoScrollPosition = New Point(0, 0)
        _card.Invalidate()
    End Sub
End Class

''' <summary>Round clock badge on the "Waiting for approval" screen.</summary>
Public Class PendingBadge
    Inherits Control

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        BackColor = Color.White
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Using b As New SolidBrush(Theme.PrimarySoft)
            g.FillEllipse(b, 0, 0, Width - 1, Height - 1)
        End Using
        Dim cx As Integer = Width \ 2
        Dim cy As Integer = Height \ 2
        Using pen As New Pen(Theme.Primary, 3.5F)
            pen.StartCap = LineCap.Round
            pen.EndCap = LineCap.Round
            g.DrawEllipse(pen, cx - 22, cy - 22, 44, 44)
            g.DrawLine(pen, cx, cy, cx, cy - 13)
            g.DrawLine(pen, cx, cy, cx + 10, cy + 6)
        End Using
    End Sub
End Class
