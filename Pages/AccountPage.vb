Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

''' <summary>Account settings for customers and sellers: profile picture, username, password. The seller's shop profile is the Store tab, not this page.</summary>
Public Class AccountPage
    Inherits PageBase

    ''' <summary>Raised after a successful save so the top bar can repaint (new name / photo).</summary>
    Public Event Saved()
    Public Event LogoutRequested()

    Private ReadOnly _accent As New Panel()
    Private ReadOnly _title As Label
    Private ReadOnly _photoCard As New RoundedPanel()
    Private ReadOnly _detailsCard As New RoundedPanel()
    Private ReadOnly _pwCard As New RoundedPanel()
    Private ReadOnly _sideCard As New RoundedPanel()
    Private ReadOnly _photoTitle As Label
    Private ReadOnly _detailsTitle As Label
    Private ReadOnly _pwTitle As Label
    Private ReadOnly _avatarBox As New AvatarBox()
    Private ReadOnly _upload As RoundedButton
    Private ReadOnly _remove As RoundedButton
    Private ReadOnly _hint As Label
    Private ReadOnly _username As New ValidatedField()
    Private ReadOnly _role As New ReadOnlyField()
    Private ReadOnly _curPw As New ValidatedField()
    Private ReadOnly _newPw As New ValidatedField()
    Private ReadOnly _confPw As New ValidatedField()
    Private ReadOnly _summary As New AccountSummary()
    Private ReadOnly _save As RoundedButton
    Private ReadOnly _line As New Panel()
    Private ReadOnly _logout As Label
    Private ReadOnly _staged As New List(Of String)
    Private _oAvatar As String = ""
    Private _avatar As String = ""
    Private _roleText As String = ""

    Public Sub New()
        _accent.BackColor = Theme.Primary
        _title = Ui.Lbl("Account settings", 28, True)

        For Each card As RoundedPanel In New RoundedPanel() {_photoCard, _detailsCard, _pwCard, _sideCard}
            card.Radius = 24
            card.FillColor = Color.White
            card.SurroundColor = Theme.Bg
            card.Shadow = True
        Next

        _roleText = If(Session.Role = "", "", Char.ToUpper(Session.Role(0)) & Session.Role.Substring(1))
        _oAvatar = ProfileSvc.MyAvatarPath()
        _avatar = _oAvatar

        ' profile picture
        _photoTitle = Ui.Lbl("Profile picture", 15, True)
        _upload = Ui.SoftBtn("Upload a photo", 190, 46)
        _remove = Ui.SoftBtn("Remove photo", 160, 40)
        _hint = Ui.Lbl("JPG, PNG or GIF. It's cropped to a circle.", 10, False, Theme.TextMuted)
        _photoCard.Controls.Add(_photoTitle)
        _photoCard.Controls.Add(_avatarBox)
        _photoCard.Controls.Add(_upload)
        _photoCard.Controls.Add(_remove)
        _photoCard.Controls.Add(_hint)

        ' details
        _detailsTitle = Ui.Lbl("Details", 15, True)
        _username.Caption = "Username"
        _username.Box.MaxLength = 20
        _username.AddRule("4-20 letters, numbers or _", Function(v As String) Validator.UsernameFormatOk(v))
        _username.Box.Text = Session.Username
        _role.Caption = "Role"
        _role.Value = _roleText
        _detailsCard.Controls.Add(_detailsTitle)
        _detailsCard.Controls.Add(_username)
        _detailsCard.Controls.Add(_role)

        ' password
        _pwTitle = Ui.Lbl("Password", 15, True)
        _curPw.Caption = "Current password"
        _curPw.IsPassword = True
        _curPw.Box.MaxLength = 64
        _newPw.Caption = "New password"
        _newPw.IsPassword = True
        _newPw.Box.MaxLength = 64
        _newPw.AddRule("6+ characters", Function(v As String) Validator.PasswordLengthOk(v))
        _newPw.AddRule("1 capital", Function(v As String) Validator.PasswordHasCapital(v))
        _newPw.AddRule("1 number", Function(v As String) Validator.PasswordHasNumber(v))
        _confPw.Caption = "Confirm new password"
        _confPw.IsPassword = True
        _confPw.Box.MaxLength = 64
        _confPw.AddRule("Matches the new password", Function(v As String) v = _newPw.Value)
        _pwCard.Controls.Add(_pwTitle)
        _pwCard.Controls.Add(_curPw)
        _pwCard.Controls.Add(_newPw)
        _pwCard.Controls.Add(_confPw)

        ' side card
        _save = Ui.PrimaryBtn("Save changes", 300, 56)
        _line.BackColor = Theme.Border
        _logout = Ui.Lbl("Log out", 11, False, Theme.TextMuted)
        _logout.Font = New Font(Theme.F(11), FontStyle.Underline)
        _logout.Cursor = Cursors.Hand
        _logout.AutoSize = False
        _logout.TextAlign = ContentAlignment.MiddleCenter
        _sideCard.Controls.Add(_summary)
        _sideCard.Controls.Add(_save)
        _sideCard.Controls.Add(_line)
        _sideCard.Controls.Add(_logout)

        Controls.Add(_accent)
        Controls.Add(_title)
        Controls.Add(_photoCard)
        Controls.Add(_detailsCard)
        Controls.Add(_pwCard)
        Controls.Add(_sideCard)

        AddHandler _upload.Click, AddressOf OnUpload
        AddHandler _remove.Click, Sub(s, e) SetAvatar("")
        AddHandler _username.ValueChanged, Sub(s, e) RefreshState()
        AddHandler _curPw.ValueChanged, Sub(s, e) RefreshState()
        AddHandler _newPw.ValueChanged, Sub(s, e)
                                            _confPw.Evaluate()
                                            RefreshState()
                                        End Sub
        AddHandler _confPw.ValueChanged, Sub(s, e) RefreshState()
        AddHandler _save.Click, AddressOf OnSave
        AddHandler _logout.Click, Sub(s, e) RaiseEvent LogoutRequested()

        RefreshVisuals()
        RefreshState()
    End Sub

    ' ---------------- state ----------------

    Private Function ShownName() As String
        Dim n As String = _username.Value.Trim()
        Return If(n = "", Session.Username, n)
    End Function

    Private Sub RefreshVisuals()
        Dim img As Image = Avatars.Load(_avatar)
        _avatarBox.SetState(ShownName(), img)
        _summary.Setup(ShownName(), _roleText, img)
        _remove.Visible = (_avatar <> "")
    End Sub

    Private Function PasswordTouched() As Boolean
        Return _curPw.Value <> "" OrElse _newPw.Value <> "" OrElse _confPw.Value <> ""
    End Function

    Private Function IsDirty() As Boolean
        Return _username.Value.Trim() <> Session.Username OrElse _avatar <> _oAvatar OrElse PasswordTouched()
    End Function

    Private Sub RefreshState()
        Dim dirty As Boolean = IsDirty()
        If _save.Enabled <> dirty Then _save.Enabled = dirty
        _summary.SetName(ShownName())
        _avatarBox.SetName(ShownName())
    End Sub

    ' ---------------- photo ----------------

    Private Sub OnUpload(sender As Object, e As EventArgs)
        Dim f As String = MenuFmt.PickImage(FindForm())
        If f = "" Then Return
        Dim err As String = ""
        Dim rel As String = ProfileSvc.SaveAvatarImage(f, err)
        If rel = "" Then
            Ui.Warn(err)
            Return
        End If
        _staged.Add(rel)
        SetAvatar(rel)
    End Sub

    Private Sub SetAvatar(rel As String)
        _avatar = rel
        RefreshVisuals()
        RefreshState()
    End Sub

    ' ---------------- save ----------------

    Private Sub OnSave(sender As Object, e As EventArgs)
        Dim newName As String = _username.Value.Trim()
        Dim renamed As Boolean = (newName <> Session.Username)
        If renamed Then
            If Not Validator.UsernameFormatOk(newName) Then
                Ui.Warn("Username must be 4-20 characters: letters, numbers or underscore.")
                Return
            End If
            Try
                If Not ProfileSvc.UsernameAvailable(newName) Then
                    Ui.Warn("That username is already taken.")
                    Return
                End If
            Catch ex As Exception
                Ui.Warn("Could not check the username: " & ex.Message)
                Return
            End Try
        End If

        Dim newPassword As String = ""
        If PasswordTouched() Then
            If _curPw.Value = "" Then
                Ui.Warn("Enter your current password to change your password.")
                Return
            End If
            Dim okNew As Boolean = _newPw.IsValid()
            Dim okConf As Boolean = _confPw.IsValid()
            If Not (okNew AndAlso okConf) Then
                Ui.Warn("Please fix the new password first (6+ characters, 1 capital, 1 number, and both boxes must match).")
                Return
            End If
            Try
                If Not ProfileSvc.VerifyPassword(_curPw.Value) Then
                    Ui.Warn("Your current password is not correct.")
                    Return
                End If
            Catch ex As Exception
                Ui.Warn("Could not check your password: " & ex.Message)
                Return
            End Try
            newPassword = _newPw.Value
        End If

        Dim err As String = ""
        Dim avatarChanged As Boolean = (_avatar <> _oAvatar)
        If Not ProfileSvc.ApplyChanges(If(renamed, newName, ""), newPassword, avatarChanged, _avatar, err) Then
            Ui.Warn(err)
            Return
        End If
        If avatarChanged Then ProfileSvc.DeleteAvatarFile(_oAvatar)
        _oAvatar = _avatar
        _staged.Clear()
        _curPw.ClearValue()
        _newPw.ClearValue()
        _confPw.ClearValue()
        _username.Box.Text = Session.Username
        RefreshVisuals()
        RefreshState()
        Toast.Show(FindForm(), "Account updated")
        RaiseEvent Saved()
    End Sub

    ' ---------------- layout ----------------

    Protected Overrides Sub LayoutPage(contentWidth As Integer)
        Dim cw As Integer = contentWidth
        _accent.SetBounds(M, 14, 46, 5)
        _title.Location = New Point(M, 26)
        Dim y As Integer = 92

        Dim twoColumns As Boolean = (cw >= 900)
        Dim sideW As Integer = 400
        Dim leftW As Integer = If(twoColumns, Math.Min(950, cw - sideW - 24), cw)

        ' profile picture
        _photoTitle.Location = New Point(28, 22)
        _avatarBox.SetBounds(28, 68, 120, 120)
        Dim bx As Integer = 28 + 120 + 28
        _upload.Location = New Point(bx, 72)
        _remove.Location = New Point(bx, 72 + 46 + 8)
        _hint.Location = New Point(bx, 72 + 46 + 8 + 40 + 8)
        _photoCard.SetBounds(M, y, leftW, 68 + 120 + 28)
        y += _photoCard.Height + 20

        ' details
        Dim inner As Integer = leftW - 56
        Dim colW As Integer = (inner - 20) \ 2
        _detailsTitle.Location = New Point(28, 22)
        _username.SetBounds(28, 68, colW, 92)
        _role.SetBounds(28 + colW + 20, 68, colW, 66)
        _detailsCard.SetBounds(M, y, leftW, 68 + 92 + 28)
        y += _detailsCard.Height + 20

        ' password
        _pwTitle.Location = New Point(28, 22)
        _curPw.SetBounds(28, 68, inner, 68)
        _newPw.SetBounds(28, 68 + 68 + 22, colW, 92)
        _confPw.SetBounds(28 + colW + 20, 68 + 68 + 22, colW, 92)
        _pwCard.SetBounds(M, y, leftW, 68 + 68 + 22 + 92 + 28)
        Dim leftBottom As Integer = y + _pwCard.Height

        ' side card
        Dim sx As Integer = If(twoColumns, M + cw - sideW, M)
        Dim sy As Integer = If(twoColumns, 92, leftBottom + 20)
        _summary.SetBounds(24, 24, sideW - 48, 230)
        _save.SetBounds(28, 24 + 230 + 14, sideW - 56, 56)
        _line.SetBounds(28, 24 + 230 + 14 + 56 + 26, sideW - 56, 1)
        _logout.SetBounds(28, 24 + 230 + 14 + 56 + 26 + 14, sideW - 56, 28)
        _sideCard.SetBounds(sx, sy, sideW, 24 + 230 + 14 + 56 + 26 + 14 + 28 + 24)
        Dim bottom As Integer = Math.Max(leftBottom, sy + _sideCard.Height)
        SetContentHeight(bottom + 40)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            For Each rel As String In _staged
                If rel <> _oAvatar Then ProfileSvc.DeleteAvatarFile(rel)
            Next
            _staged.Clear()
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class

''' <summary>Editable profile picture preview: the photo (no letter) or the letter circle when there is none.</summary>
Public Class AvatarBox
    Inherits Control

    Private _name As String = ""
    Private _img As Image

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, False)
        BackColor = Color.White
    End Sub

    Public Sub SetState(name As String, img As Image)
        _name = name
        _img = img
        Invalidate()
    End Sub

    Public Sub SetName(name As String)
        If _name = name Then Return
        _name = name
        If _img Is Nothing Then Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        Dim r As New Rectangle(0, 0, Width - 1, Height - 1)
        If _img IsNot Nothing Then
            Avatars.DrawImageCircle(g, r, _img)
        Else
            Avatars.DrawLetter(g, r, _name)
        End If
    End Sub
End Class

''' <summary>Name, role pill and big avatar shown at the top of the right-hand card.</summary>
Public Class AccountSummary
    Inherits Control

    Private _name As String = ""
    Private _role As String = ""
    Private _img As Image

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, False)
        BackColor = Color.White
    End Sub

    Public Sub Setup(name As String, role As String, img As Image)
        _name = name
        _role = role
        _img = img
        Invalidate()
    End Sub

    Public Sub SetName(name As String)
        If _name = name Then Return
        _name = name
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim d As Integer = 106
        Dim r As New Rectangle((Width - d) \ 2, 6, d, d)
        If _img IsNot Nothing Then
            Avatars.DrawImageCircle(g, r, _img)
        Else
            Avatars.DrawLetter(g, r, _name)
        End If
        TextRenderer.DrawText(g, _name, Theme.F(18, True), New Rectangle(0, 128, Width, 36), Theme.TextDark,
                              TextFormatFlags.NoPadding Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix)
        If _role <> "" Then
            Dim pf As Font = Theme.F(10, True)
            Dim pw As Integer = TextRenderer.MeasureText(_role, pf, New Size(300, 30), TextFormatFlags.NoPadding).Width + 32
            Dim pr As New Rectangle((Width - pw) \ 2, 176, pw, 30)
            Using p As GraphicsPath = Theme.RoundRectPath(pr, 15), b As New SolidBrush(Theme.PrimarySoft)
                g.FillPath(b, p)
            End Using
            TextRenderer.DrawText(g, _role, pf, pr, Theme.Primary, TextFormatFlags.NoPadding Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)
        End If
    End Sub
End Class

''' <summary>Caption + greyed-out box with a lock (for the Role).</summary>
Public Class ReadOnlyField
    Inherits Control

    Private _caption As String = ""
    Private _value As String = ""

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, False)
        BackColor = Color.White
    End Sub

    Public Property Caption As String
        Get
            Return _caption
        End Get
        Set(v As String)
            _caption = v
            Invalidate()
        End Set
    End Property

    Public Property Value As String
        Get
            Return _value
        End Get
        Set(v As String)
            _value = v
            Invalidate()
        End Set
    End Property

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        TextRenderer.DrawText(g, _caption, Theme.F(9.5F, True), New Rectangle(0, 0, Width, 20), Theme.TextDark, TextFormatFlags.NoPadding Or TextFormatFlags.VerticalCenter)
        Dim box As New Rectangle(1, 23, Width - 3, 41)
        Using p As GraphicsPath = Theme.RoundRectPath(box, 11), b As New SolidBrush(Theme.FieldBg), pen As New Pen(Theme.Border, 1.5F)
            g.FillPath(b, p)
            g.DrawPath(pen, p)
        End Using
        TextRenderer.DrawText(g, _value, Theme.F(10.5F), New Rectangle(14, box.Y, Width - 70, box.Height), Theme.TextMuted,
                              TextFormatFlags.NoPadding Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis)
        Dim lx As Integer = Width - 38
        Dim ly As Integer = box.Y + (box.Height - 18) \ 2
        Using b As New SolidBrush(Theme.TextMuted), pen As New Pen(Theme.TextMuted, 2.0F)
            g.DrawArc(pen, lx + 3, ly, 10, 12, 180, 180)
            Using bp As GraphicsPath = Theme.RoundRectPath(New Rectangle(lx, ly + 7, 16, 11), 3)
                g.FillPath(b, bp)
            End Using
        End Using
    End Sub
End Class
