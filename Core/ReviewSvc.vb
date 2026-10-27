Imports System.Data
Imports System.Data.OleDb

''' <summary>Star counts for a product (or for a whole stall).</summary>
Public Class ReviewSummary
    Public Total As Integer
    Public Avg As Double
    ''' <summary>Counts(1) .. Counts(5) = number of reviews with that many stars.</summary>
    Public Counts(5) As Integer
End Class

''' <summary>Product reviews: a customer can review an item once per COMPLETED order that contains it; the stall can reply once to each review.</summary>
Public Module ReviewSvc
    Private Function BuildSummary(dt As DataTable) As ReviewSummary
        Dim s As New ReviewSummary()
        Dim sum As Integer = 0
        For Each r As DataRow In dt.Rows
            Dim star As Integer = Db.GetInt(r, "rating")
            Dim c As Integer = Db.GetInt(r, "c")
            If star >= 1 AndAlso star <= 5 Then
                s.Counts(star) = c
                s.Total += c
                sum += star * c
            End If
        Next
        If s.Total > 0 Then s.Avg = sum / s.Total
        Return s
    End Function

    Public Function GetSummary(productId As Integer) As ReviewSummary
        Return BuildSummary(Db.Query("SELECT rating, COUNT(*) AS c FROM reviews WHERE product_id=@p GROUP BY rating",
                                     Db.P("@p", productId)))
    End Function

    Public Function GetStallSummary(stallId As Integer) As ReviewSummary
        Return BuildSummary(Db.Query("SELECT r.rating, COUNT(*) AS c FROM reviews r INNER JOIN products p ON p.id=r.product_id " &
                                     "WHERE p.stall_id=@s GROUP BY r.rating", Db.P("@s", stallId)))
    End Function

    ''' <summary>Reviews of one product: the signed-in customer's own review first, then newest first.</summary>
    Public Function GetReviews(productId As Integer) As DataTable
        ' the signed-in user's own review sorts first (their id is a number, so it is safe to put in the SQL text)
        Dim myId As Integer = CInt(Session.UserId)
        Return Db.Query("SELECT TOP 60 r.id, r.customer_id, u.username, r.rating, r.[comment], r.created_at, r.reply, r.replied_at " &
                        "FROM reviews r INNER JOIN users u ON u.id=r.customer_id " &
                        "WHERE r.product_id=@p ORDER BY IIf(r.customer_id=" & myId.ToString() & ", 0, 1), r.created_at DESC, r.id DESC",
                        Db.P("@p", productId))
    End Function

    ''' <summary>
    ''' The customer's most recent COMPLETED order (order_stalls.id) that contains this product and has no review yet.
    ''' 0 = the customer has not ordered it (or has already reviewed every order), so they cannot write a new review.
    ''' </summary>
    Public Function GetReviewableOrder(productId As Integer) As Integer
        If Session.Role <> "customer" Then Return 0
        Dim o As Object = Db.Scalar("SELECT TOP 1 os.id FROM order_stalls os INNER JOIN orders o ON o.id=os.order_id " &
                                    "WHERE o.customer_id=@u AND os.[status]='completed' " &
                                    "AND EXISTS (SELECT oi.id FROM order_items oi WHERE oi.order_stall_id=os.id AND oi.product_id=@p) " &
                                    "AND NOT EXISTS (SELECT r.id FROM reviews r WHERE r.customer_id=@u AND r.product_id=@p AND r.order_stall_id=os.id) " &
                                    "ORDER BY os.id DESC",
                                    Db.P("@u", Session.UserId), Db.P("@p", productId))
        If o Is Nothing OrElse IsDBNull(o) Then Return 0
        Return Convert.ToInt32(o)
    End Function

    Private Function CleanComment(comment As String) As Object
        Dim c As String = If(comment, "").Trim()
        If c.Length > 255 Then c = c.Substring(0, 255)
        Return If(c = "", CObj(DBNull.Value), CObj(c))
    End Function

    ''' <summary>Adds a review for one completed order. Each completed order of the item can be reviewed once.</summary>
    Public Function AddReview(productId As Integer, orderStallId As Integer, rating As Integer, comment As String, ByRef err As String) As Boolean
        err = ""
        If rating < 1 OrElse rating > 5 Then
            err = "Please choose a star rating."
            Return False
        End If
        If orderStallId <= 0 OrElse GetReviewableOrder(productId) = 0 Then
            err = "You can only review items from your completed orders."
            Return False
        End If
        ' the order must be the customer's own, completed, and contain the item
        Dim ok As Integer = Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM order_stalls os INNER JOIN orders o ON o.id=os.order_id " &
                                                      "WHERE os.id=@os AND o.customer_id=@u AND os.[status]='completed' " &
                                                      "AND EXISTS (SELECT oi.id FROM order_items oi WHERE oi.order_stall_id=os.id AND oi.product_id=@p)",
                                                      Db.P("@os", orderStallId), Db.P("@u", Session.UserId), Db.P("@p", productId)))
        If ok = 0 Then
            err = "You can only review items from your completed orders."
            Return False
        End If
        Dim dup As Integer = Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM reviews WHERE customer_id=@u AND product_id=@p AND order_stall_id=@os",
                                                       Db.P("@u", Session.UserId), Db.P("@p", productId), Db.P("@os", orderStallId)))
        If dup > 0 Then
            err = "You already reviewed that order."
            Return False
        End If
        Try
            Db.Exec("INSERT INTO reviews (product_id, customer_id, order_stall_id, rating, [comment], created_at) VALUES (@p,@u,@os,@r,@c,@t)",
                    Db.P("@p", productId), Db.P("@u", Session.UserId), Db.P("@os", orderStallId),
                    Db.P("@r", rating), Db.P("@c", CleanComment(comment)), Db.P("@t", DateTime.Now))
        Catch ex As OleDbException
            err = "You already reviewed that order."
            Return False
        End Try
        Return True
    End Function

    ''' <summary>Edits one of the customer's own reviews. A reply the stall already wrote stays.</summary>
    Public Function UpdateReview(reviewId As Integer, rating As Integer, comment As String, ByRef err As String) As Boolean
        err = ""
        If rating < 1 OrElse rating > 5 Then
            err = "Please choose a star rating."
            Return False
        End If
        Db.Exec("UPDATE reviews SET rating=@r, [comment]=@c WHERE id=@i AND customer_id=@u",
                Db.P("@r", rating), Db.P("@c", CleanComment(comment)), Db.P("@i", reviewId), Db.P("@u", Session.UserId))
        Return True
    End Function

    Public Sub DeleteReview(reviewId As Integer)
        Db.Exec("DELETE FROM reviews WHERE id=@i AND customer_id=@u", Db.P("@i", reviewId), Db.P("@u", Session.UserId))
    End Sub

    ' ====================== seller side ======================
    ''' <summary>All reviews of the stall's items, newest first.</summary>
    Public Function GetStallReviews(stallId As Integer) As DataTable
        Return Db.Query("SELECT TOP 300 r.id, r.product_id, p.[name] AS product_name, p.image_path, u.username, r.rating, r.[comment], " &
                        "r.created_at, r.reply, r.replied_at " &
                        "FROM ((reviews r INNER JOIN products p ON p.id=r.product_id) INNER JOIN users u ON u.id=r.customer_id) " &
                        "INNER JOIN stalls s ON s.id=p.stall_id " &
                        "WHERE p.stall_id=@s AND s.owner_id=@o ORDER BY r.created_at DESC, r.id DESC",
                        Db.P("@s", stallId), Db.P("@o", Session.UserId))
    End Function

    ''' <summary>The stall replies to a review. Only once: a review that already has a reply is refused.</summary>
    Public Function ReplyTo(reviewId As Integer, text As String, ByRef err As String) As Boolean
        err = ""
        Dim t As String = If(text, "").Trim()
        If t = "" Then
            err = "Write a reply first."
            Return False
        End If
        If t.Length > 255 Then t = t.Substring(0, 255)
        Dim n As Integer = Db.Exec("UPDATE reviews SET reply=@t, replied_at=@d " &
                                   "WHERE id=@i AND reply IS NULL AND product_id IN " &
                                   "(SELECT p.id FROM products p INNER JOIN stalls s ON s.id=p.stall_id WHERE s.owner_id=@o)",
                                   Db.P("@t", t), Db.P("@d", DateTime.Now), Db.P("@i", reviewId), Db.P("@o", Session.UserId))
        If n = 0 Then
            err = "This review already has a reply (or it was removed)."
            Return False
        End If
        Return True
    End Function
End Module
