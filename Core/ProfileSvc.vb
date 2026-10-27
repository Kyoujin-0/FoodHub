Imports System.Data
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Windows.Forms

''' <summary>Account settings of the logged-in user (customer or seller): photo, username, password.</summary>
Public Module ProfileSvc
    Private _mineFor As Integer = -1
    Private _minePath As String = ""

    ''' <summary>Stored profile photo path of the logged-in user ("" when none). Cached; call ForgetMine after changing it.</summary>
    Public Function MyAvatarPath() As String
        If Session.UserId = 0 Then Return ""
        If _mineFor <> Session.UserId Then
            Try
                Dim o As Object = Db.Scalar("SELECT avatar_path FROM users WHERE id=@u", Db.P("@u", Session.UserId))
                _minePath = If(o Is Nothing OrElse IsDBNull(o), "", Convert.ToString(o))
            Catch
                _minePath = ""
            End Try
            _mineFor = Session.UserId
        End If
        Return _minePath
    End Function

    Public Sub ForgetMine()
        _mineFor = -1
    End Sub

    Public Function UsernameAvailable(name As String) As Boolean
        Dim n As Object = Db.Scalar("SELECT COUNT(*) FROM users WHERE username=@n AND id<>@u", Db.P("@n", name), Db.P("@u", Session.UserId))
        Return Convert.ToInt32(n) = 0
    End Function

    Public Function VerifyPassword(password As String) As Boolean
        Dim o As Object = Db.Scalar("SELECT password_hash FROM users WHERE id=@u", Db.P("@u", Session.UserId))
        If o Is Nothing OrElse IsDBNull(o) Then Return False
        Return BCrypt.Net.BCrypt.Verify(password, Convert.ToString(o))
    End Function

    ''' <summary>Applies the changes. newUsername / newPassword "" = unchanged. setAvatar = True writes avatarPath ("" removes the photo).</summary>
    Public Function ApplyChanges(newUsername As String, newPassword As String, setAvatar As Boolean, avatarPath As String, ByRef errMsg As String) As Boolean
        Try
            If newUsername <> "" AndAlso newUsername <> Session.Username Then
                Db.Exec("UPDATE users SET username=@n WHERE id=@u", Db.P("@n", newUsername), Db.P("@u", Session.UserId))
                If Session.LoadRemembered() = Session.Username Then Session.SaveRemembered(newUsername)
                Session.Username = newUsername
            End If
            If newPassword <> "" Then
                Dim hash As String = BCrypt.Net.BCrypt.HashPassword(newPassword, 10)
                Db.Exec("UPDATE users SET password_hash=@p WHERE id=@u", Db.P("@p", hash), Db.P("@u", Session.UserId))
            End If
            If setAvatar Then
                Db.Exec("UPDATE users SET avatar_path=@a WHERE id=@u", Db.P("@a", If(If(avatarPath, "") = "", Nothing, avatarPath)), Db.P("@u", Session.UserId))
                ForgetMine()
            End If
            Return True
        Catch ex As Exception
            errMsg = "Could not save your changes: " & ex.Message
            Return False
        End Try
    End Function

    ''' <summary>Crops the picked photo to a square (max 512 px), stores it as JPEG in images\avatars. Returns the relative path or "" with errMsg.</summary>
    Public Function SaveAvatarImage(sourceFile As String, ByRef errMsg As String) As String
        Try
            If New FileInfo(sourceFile).Length > InboxSvc.MaxImageBytes Then
                errMsg = "That photo is too big (max 20 MB)."
                Return ""
            End If
            Using src As Image = InboxSvc.LoadImgFile(sourceFile)
                If src Is Nothing Then
                    errMsg = "That file is not a valid image."
                    Return ""
                End If
                Dim side As Integer = Math.Min(src.Width, src.Height)
                Dim target As Integer = Math.Min(512, side)
                Using bmp As New Bitmap(target, target)
                    Using g As Graphics = Graphics.FromImage(bmp)
                        g.Clear(Color.White)
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic
                        g.DrawImage(src, New Rectangle(0, 0, target, target), New Rectangle((src.Width - side) \ 2, (src.Height - side) \ 2, side, side), GraphicsUnit.Pixel)
                    End Using
                    Dim dir As String = Path.Combine(Db.DataFolder(), "images", "avatars")
                    Directory.CreateDirectory(dir)
                    Dim fname As String = "u" & Guid.NewGuid().ToString("N") & ".jpg"
                    bmp.Save(Path.Combine(dir, fname), ImageFormat.Jpeg)
                    Return "images/avatars/" & fname
                End Using
            End Using
        Catch ex As Exception
            errMsg = "Could not save the photo: " & ex.Message
            Return ""
        End Try
    End Function

    Public Sub DeleteAvatarFile(relPath As String)
        Try
            If String.IsNullOrEmpty(relPath) OrElse Not relPath.StartsWith("images/avatars/") Then Return
            Dim full As String = Path.Combine(Db.DataFolder(), relPath.Replace("/", "\"))
            If File.Exists(full) Then File.Delete(full)
        Catch
        End Try
    End Sub
End Module

''' <summary>Round profile pictures everywhere: the photo when there is one (no letter), otherwise a coloured circle with the first letter.</summary>
Public Module Avatars
    Private ReadOnly _imgs As New Dictionary(Of String, Image)
    Private ReadOnly _byName As New Dictionary(Of String, KeyValuePair(Of DateTime, String))

    ''' <summary>Shared cached image of a stored photo (do NOT dispose it). Nothing when there is none.</summary>
    Public Function Load(storedPath As String) As Image
        If If(storedPath, "") = "" Then Return Nothing
        If _imgs.ContainsKey(storedPath) Then Return _imgs(storedPath)
        Dim img As Image = InboxSvc.LoadImg(storedPath)
        _imgs(storedPath) = img
        Return img
    End Function

    ''' <summary>Photo path of a user by username (cached for a minute, so other people's changes show up soon).</summary>
    Public Function PathOfUser(name As String) As String
        If If(name, "") = "" Then Return ""
        If name = Session.Username Then Return ProfileSvc.MyAvatarPath()
        Dim e As KeyValuePair(Of DateTime, String)
        If _byName.TryGetValue(name, e) AndAlso (DateTime.Now - e.Key).TotalSeconds < 60 Then Return e.Value
        Dim p As String = ""
        Try
            Dim o As Object = Db.Scalar("SELECT avatar_path FROM users WHERE username=@n", Db.P("@n", name))
            If o IsNot Nothing AndAlso Not IsDBNull(o) Then p = Convert.ToString(o)
        Catch
        End Try
        _byName(name) = New KeyValuePair(Of DateTime, String)(DateTime.Now, p)
        Return p
    End Function

    Public Sub Draw(g As Graphics, r As Rectangle, name As String, storedPath As String)
        Dim img As Image = Load(storedPath)
        If img IsNot Nothing Then
            DrawImageCircle(g, r, img)
        Else
            DrawLetter(g, r, name)
        End If
    End Sub

    Public Sub DrawImageCircle(g As Graphics, r As Rectangle, img As Image)
        Dim st As GraphicsState = g.Save()
        g.SmoothingMode = SmoothingMode.AntiAlias
        Using p As New GraphicsPath()
            p.AddEllipse(r)
            g.SetClip(p)
        End Using
        g.InterpolationMode = InterpolationMode.HighQualityBicubic
        Dim s As Integer = Math.Min(img.Width, img.Height)
        g.DrawImage(img, r, New Rectangle((img.Width - s) \ 2, (img.Height - s) \ 2, s, s), GraphicsUnit.Pixel)
        g.Restore(st)
    End Sub

    Public Sub DrawLetter(g As Graphics, r As Rectangle, name As String)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Using b As New SolidBrush(InboxUi.AvatarColor(name))
            g.FillEllipse(b, r)
        End Using
        Dim initial As String = If(If(name, "") <> "", name.Substring(0, 1).ToUpper(), "?")
        TextRenderer.DrawText(g, initial, Theme.F(CSng(r.Height * 0.4), True), r, Color.White,
                              TextFormatFlags.NoPadding Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)
    End Sub
End Module
