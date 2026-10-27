Imports System.Data
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Windows.Forms

''' <summary>Messages between customers and stalls: conversation lists, threads, sending (text or photo), read flags, 15-day cleanup.</summary>
Public Module InboxSvc
    Public Const KeepDays As Integer = 15
    Public Const MaxImageBytes As Long = 20L * 1024L * 1024L

    Private Const ActiveSql As String = "os.[status] IN ('pending','preparing','ready')"
    Private Const FinishedSql As String = "os.[status] IN ('completed','cancelled')"

    ' ---------------- lists (seller) ----------------

    ''' <summary>Conversations of the stall that have at least one message. finished = False gives running orders, True gives completed/declined ones.</summary>
    Public Function GetConversations(stallId As Integer, finished As Boolean) As DataTable
        Dim cond As String = If(finished, FinishedSql, ActiveSql)
        ' only orders that have at least one message; the last message's fields are filled in below
        Dim dt As DataTable = Db.Query("SELECT os.id, os.order_id, u.username, os.[status], IIf(os.completed_at IS NULL, os.updated_at, os.completed_at) AS done_at, " &
                        "(SELECT MAX(m2.id) FROM messages m2 WHERE m2.order_stall_id=os.id) AS last_id, " &
                        "(SELECT COUNT(*) FROM messages x WHERE x.order_stall_id=os.id AND x.sender_id<>@me AND x.is_read=0) AS unread " &
                        "FROM (order_stalls os INNER JOIN orders o ON o.id=os.order_id) INNER JOIN users u ON u.id=o.customer_id " &
                        "WHERE os.stall_id=@s AND " & cond & " AND os.id IN (SELECT m3.order_stall_id FROM messages m3)",
                        Db.P("@me", Session.UserId), Db.P("@s", stallId))
        dt.Columns.Add("body", GetType(String))
        dt.Columns.Add("image_path", GetType(String))
        dt.Columns.Add("sender_id", GetType(Integer))
        dt.Columns.Add("sent_at", GetType(DateTime))
        If dt.Rows.Count > 0 Then
            Dim ids As New List(Of String)
            For Each r As DataRow In dt.Rows
                ids.Add(Db.GetInt(r, "last_id").ToString())
            Next
            Dim msgs As DataTable = Db.Query("SELECT id, body, image_path, sender_id, sent_at FROM messages WHERE id IN (" & String.Join(",", ids) & ")")
            For Each r As DataRow In dt.Rows
                For Each m As DataRow In msgs.Rows
                    If Db.GetInt(m, "id") = Db.GetInt(r, "last_id") Then
                        r("body") = Db.GetStr(m, "body")
                        r("image_path") = If(IsDBNull(m("image_path")), DBNull.Value, m("image_path"))
                        r("sender_id") = Db.GetInt(m, "sender_id")
                        r("sent_at") = m("sent_at")
                        Exit For
                    End If
                Next
            Next
        End If
        dt.DefaultView.Sort = "last_id DESC"
        Return dt.DefaultView.ToTable()
    End Function

    ''' <summary>Orders of the stall that are still running (for the Open orders popup).</summary>
    Public Function GetOpenOrders(stallId As Integer) As DataTable
        Dim dt As DataTable = Db.Query("SELECT os.id, os.order_id, u.username, os.[status], o.created_at, os.subtotal, " &
                        "(SELECT COUNT(*) FROM messages m WHERE m.order_stall_id=os.id) AS msg_count " &
                        "FROM (order_stalls os INNER JOIN orders o ON o.id=os.order_id) INNER JOIN users u ON u.id=o.customer_id " &
                        "WHERE os.stall_id=@s AND " & ActiveSql & " ORDER BY os.id", Db.P("@s", stallId))
        Db.AttachJoinedText(dt, "id", "items",
                            "SELECT i.order_stall_id AS k, (i.quantity & ' x ' & i.product_name) AS t FROM order_items i WHERE i.order_stall_id IN ({IDS}) ORDER BY i.id", "k", "t")
        Return dt
    End Function

    ' ---------------- one conversation (seller and customer) ----------------

    ''' <summary>Header info of a conversation, or Nothing when the logged-in user is not part of it.</summary>
    Public Function GetThreadInfo(orderStallId As Integer) As DataRow
        Dim dt As DataTable = Db.Query("SELECT os.id, os.order_id, os.[status], u.username, s.[name] AS stall_name " &
                                       "FROM ((order_stalls os INNER JOIN orders o ON o.id=os.order_id) INNER JOIN users u ON u.id=o.customer_id) INNER JOIN stalls s ON s.id=os.stall_id " &
                                       "WHERE os.id=@os AND (s.owner_id=@u OR o.customer_id=@u)", Db.P("@os", orderStallId), Db.P("@u", Session.UserId))
        If dt.Rows.Count = 0 Then Return Nothing
        Return dt.Rows(0)
    End Function

    Public Function GetThread(orderStallId As Integer) As DataTable
        Return Db.Query("SELECT m.id, m.sender_id, m.body, m.image_path, m.sent_at " &
                        "FROM ((messages m INNER JOIN order_stalls os ON os.id=m.order_stall_id) INNER JOIN orders o ON o.id=os.order_id) INNER JOIN stalls s ON s.id=os.stall_id " &
                        "WHERE m.order_stall_id=@os AND (s.owner_id=@u OR o.customer_id=@u) ORDER BY m.id",
                        Db.P("@os", orderStallId), Db.P("@u", Session.UserId))
    End Function

    ''' <summary>Sends a text and/or photo. Refused when the order is finished or the user is not part of it.</summary>
    Public Function Send(orderStallId As Integer, body As String, imagePath As String, ByRef errMsg As String) As Boolean
        Try
            ' the order must still be running and the user must be its customer or its stall owner
            Dim ok As Integer = Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM (order_stalls os INNER JOIN orders o ON o.id=os.order_id) INNER JOIN stalls s ON s.id=os.stall_id " &
                                       "WHERE os.id=@os AND os.[status] IN ('pending','preparing','ready') AND (s.owner_id=@u OR o.customer_id=@u)",
                                       Db.P("@os", orderStallId), Db.P("@u", Session.UserId)))
            If ok = 0 Then
                errMsg = "This order is finished, so new messages can't be sent."
                Return False
            End If
            ' a photo-only message has no text: stored as empty (NULL), Access does not like zero-length text
            Db.Exec("INSERT INTO messages (order_stall_id, sender_id, body, image_path, is_read, sent_at) VALUES (@os,@u,@b,@i,0,@t)",
                    Db.P("@os", orderStallId), Db.P("@u", Session.UserId), Db.P("@b", If(If(body, "") = "", Nothing, body)),
                    Db.P("@i", If(If(imagePath, "") = "", Nothing, imagePath)), Db.P("@t", DateTime.Now))
            Return True
        Catch ex As Exception
            errMsg = "Could not send: " & ex.Message
            Return False
        End Try
    End Function

    Public Sub MarkRead(orderStallId As Integer)
        Try
            Db.Exec("UPDATE messages SET is_read=1 WHERE order_stall_id=@os AND sender_id<>@u AND is_read=0", Db.P("@os", orderStallId), Db.P("@u", Session.UserId))
        Catch
        End Try
    End Sub

    ' ---------------- 15-day cleanup ----------------

    ''' <summary>Deletes the messages (and photo files) of orders that finished more than KeepDays days ago.</summary>
    Public Sub PurgeOld()
        Try
            Dim old As String = "order_stall_id IN (SELECT id FROM order_stalls WHERE [status] IN ('completed','cancelled') AND " &
                                "IIf(completed_at IS NULL, updated_at, completed_at) < @cut)"
            Dim cut As OleDb.OleDbParameter = Db.P("@cut", DateTime.Now.AddDays(-KeepDays))
            Dim dt As DataTable = Db.Query("SELECT id, image_path FROM messages WHERE " & old, cut)
            If dt.Rows.Count = 0 Then Return
            For Each r As DataRow In dt.Rows
                Dim p As String = Db.GetStr(r, "image_path")
                If p <> "" Then DeleteImageFile(p)
            Next
            Db.Exec("DELETE FROM messages WHERE " & old, cut)
        Catch
        End Try
    End Sub

    ' ---------------- photos ----------------

    Private Function ImagesDir() As String
        Dim d As String = Path.Combine(Db.DataFolder(), "images", "messages")
        Directory.CreateDirectory(d)
        Return d
    End Function

    Public Function ResolvePath(path1 As String) As String
        If Path.IsPathRooted(path1) Then Return path1
        Return Path.Combine(Db.DataFolder(), path1.Replace("/", "\"))
    End Function

    ''' <summary>Reads an image without keeping the file locked. Nothing when it is missing or not an image.</summary>
    Public Function LoadImgFile(fullPath As String) As Image
        Try
            Dim bytes As Byte() = File.ReadAllBytes(fullPath)
            Using ms As New MemoryStream(bytes)
                Using src As Image = Image.FromStream(ms)
                    Return New Bitmap(src)
                End Using
            End Using
        Catch
            Return Nothing
        End Try
    End Function

    ''' <summary>Loads a stored message photo (relative path like images/messages/x.jpg).</summary>
    Public Function LoadImg(storedPath As String) As Image
        If If(storedPath, "") = "" Then Return Nothing
        Return LoadImgFile(ResolvePath(storedPath))
    End Function

    Public Sub DeleteImageFile(storedPath As String)
        Try
            Dim p As String = ResolvePath(storedPath)
            If File.Exists(p) Then File.Delete(p)
        Catch
        End Try
    End Sub

    ''' <summary>Resizes (max 1100 px, JPEG) and stores the picked photo. Returns the stored path, or "" with errMsg set.</summary>
    Public Function SaveImage(sourceFile As String, ByRef errMsg As String) As String
        Try
            If New FileInfo(sourceFile).Length > MaxImageBytes Then
                errMsg = "That photo is too big (max 20 MB)."
                Return ""
            End If
            Using src As Image = LoadImgFile(sourceFile)
                If src Is Nothing Then
                    errMsg = "That file is not a valid image."
                    Return ""
                End If
                Dim sc As Double = Math.Min(1.0, 1100.0 / Math.Max(src.Width, src.Height))
                Dim w As Integer = Math.Max(1, CInt(src.Width * sc))
                Dim h As Integer = Math.Max(1, CInt(src.Height * sc))
                Using bmp As New Bitmap(w, h)
                    Using g As Graphics = Graphics.FromImage(bmp)
                        g.Clear(Color.White)
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic
                        g.DrawImage(src, 0, 0, w, h)
                    End Using
                    Dim fname As String = "m" & DateTime.Now.ToString("yyyyMMddHHmmssfff") & Guid.NewGuid().ToString("N").Substring(0, 6) & ".jpg"
                    SaveJpeg(bmp, Path.Combine(ImagesDir(), fname), 82L)
                    Return "images/messages/" & fname
                End Using
            End Using
        Catch ex As Exception
            errMsg = "Could not save the photo: " & ex.Message
            Return ""
        End Try
    End Function

    Private Sub SaveJpeg(bmp As Bitmap, fullPath As String, quality As Long)
        Dim codec As ImageCodecInfo = Nothing
        For Each c As ImageCodecInfo In ImageCodecInfo.GetImageEncoders()
            If c.MimeType = "image/jpeg" Then codec = c
        Next
        If codec Is Nothing Then
            bmp.Save(fullPath, ImageFormat.Jpeg)
            Return
        End If
        Using ep As New EncoderParameters(1)
            ep.Param(0) = New EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality)
            bmp.Save(fullPath, codec, ep)
        End Using
    End Sub
End Module
