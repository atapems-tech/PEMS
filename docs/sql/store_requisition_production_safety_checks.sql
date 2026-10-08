-- Store Requisition / Inventory production safety checks (READ-ONLY). Run on the target database BEFORE revalidating constraints.
-- 24 checks. Results: server/database/timestamp, a per-check violation count, then the affected ids. Nothing here modifies data (only a #temp table).
SET NOCOUNT ON;

IF OBJECT_ID('tempdb..#viol') IS NOT NULL DROP TABLE #viol;
CREATE TABLE #viol (chk nvarchar(200) NOT NULL, id int NULL);
INSERT INTO #viol (chk, id)
SELECT 'S1 stock negative OnHand' AS chk, MaterialStockId AS id FROM MaterialStocks WHERE OnHandQuantity < 0
UNION ALL SELECT 'S2 stock negative Reserved', MaterialStockId FROM MaterialStocks WHERE ReservedQuantity < 0
UNION ALL SELECT 'S3 stock Reserved > OnHand', MaterialStockId FROM MaterialStocks WHERE ReservedQuantity > OnHandQuantity
UNION ALL SELECT 'R1 line Issued < 0', StoreRequisitionLineId FROM StoreRequisitionLines WHERE IssuedQuantity < 0
UNION ALL SELECT 'R2 line Approved < 0', StoreRequisitionLineId FROM StoreRequisitionLines WHERE ApprovedQuantity < 0
UNION ALL SELECT 'R3 line Approved > Requested', StoreRequisitionLineId FROM StoreRequisitionLines WHERE ApprovedQuantity > RequestedQuantity
UNION ALL SELECT 'R4 line Issued > COALESCE(Approved,Requested)', StoreRequisitionLineId FROM StoreRequisitionLines WHERE IssuedQuantity > COALESCE(ApprovedQuantity, RequestedQuantity)
UNION ALL SELECT 'R5 line Issued > Requested', StoreRequisitionLineId FROM StoreRequisitionLines WHERE IssuedQuantity > RequestedQuantity
UNION ALL SELECT 'V1 reservation Quantity < 0', MaterialReservationId FROM MaterialReservations WHERE Quantity < 0
UNION ALL SELECT 'V2 reservation Issued > Quantity', MaterialReservationId FROM MaterialReservations WHERE IssuedQuantity > Quantity
UNION ALL SELECT 'V3 reservation -> requisition missing/inactive', r.MaterialReservationId FROM MaterialReservations r
        LEFT JOIN StoreRequisitions s ON s.StoreRequisitionId = r.StoreRequisitionId
        WHERE r.StoreRequisitionId IS NOT NULL AND (s.StoreRequisitionId IS NULL OR s.IsActive = 0)
UNION ALL SELECT 'V4 reservation -> line missing/inactive', r.MaterialReservationId FROM MaterialReservations r
        LEFT JOIN StoreRequisitionLines l ON l.StoreRequisitionLineId = r.StoreRequisitionLineId
        WHERE r.StoreRequisitionLineId IS NOT NULL AND (l.StoreRequisitionLineId IS NULL OR l.IsActive = 0)
UNION ALL SELECT 'V5 reservation line belongs to another requisition', r.MaterialReservationId FROM MaterialReservations r
        JOIN StoreRequisitionLines l ON l.StoreRequisitionLineId = r.StoreRequisitionLineId
        WHERE r.StoreRequisitionId IS NOT NULL AND l.StoreRequisitionId <> r.StoreRequisitionId
UNION ALL SELECT 'V6 reservation material differs from its line', r.MaterialReservationId FROM MaterialReservations r
        JOIN StoreRequisitionLines l ON l.StoreRequisitionLineId = r.StoreRequisitionLineId WHERE l.MaterialItemId <> r.MaterialItemId
UNION ALL SELECT 'V7 duplicate active reservation per line', MIN(MaterialReservationId) FROM MaterialReservations
        WHERE StoreRequisitionLineId IS NOT NULL AND Status IN ('Active','PartiallyIssued') GROUP BY StoreRequisitionLineId HAVING COUNT(*) > 1
UNION ALL SELECT 'V8 legacy reservation without line (store requisition)', MaterialReservationId FROM MaterialReservations
        WHERE StoreRequisitionId IS NOT NULL AND StoreRequisitionLineId IS NULL AND Status IN ('Active','PartiallyIssued')
UNION ALL SELECT 'V9 legacy reservation AMBIGUOUS (>1 or 0 matching lines)', r.MaterialReservationId FROM MaterialReservations r
        WHERE r.StoreRequisitionId IS NOT NULL AND r.StoreRequisitionLineId IS NULL AND r.Status IN ('Active','PartiallyIssued')
          AND (SELECT COUNT(*) FROM StoreRequisitionLines l WHERE l.StoreRequisitionId = r.StoreRequisitionId AND l.MaterialItemId = r.MaterialItemId AND l.IsActive = 1) <> 1
UNION ALL SELECT 'V10 active reservations exceed stock reserved (per stock row)', s.MaterialStockId FROM MaterialStocks s
        WHERE (SELECT COALESCE(SUM(r.Quantity - r.IssuedQuantity),0) FROM MaterialReservations r
                WHERE r.MaterialItemId = s.MaterialItemId AND r.WarehouseId = s.WarehouseId AND r.Status IN ('Active','PartiallyIssued')) > s.ReservedQuantity
UNION ALL SELECT 'T1 Issue transaction with missing material', t.InventoryTransactionId FROM InventoryTransactions t
        LEFT JOIN MaterialItems m ON m.MaterialItemId = t.MaterialItemId WHERE t.TransactionType = 'Issue' AND m.MaterialItemId IS NULL
UNION ALL SELECT 'T2 StoreRequisition Issue with invalid source requisition', t.InventoryTransactionId FROM InventoryTransactions t
        LEFT JOIN StoreRequisitions s ON s.StoreRequisitionId = t.ReferenceId
        WHERE t.TransactionType = 'Issue' AND t.ReferenceType = 'StoreRequisition' AND s.StoreRequisitionId IS NULL
UNION ALL SELECT 'T3 Issue total per requisition+material exceeds issued on lines', x.StoreRequisitionId FROM (
        SELECT t.ReferenceId AS StoreRequisitionId, t.MaterialItemId, SUM(t.Quantity) AS q FROM InventoryTransactions t
         WHERE t.TransactionType = 'Issue' AND t.ReferenceType = 'StoreRequisition' GROUP BY t.ReferenceId, t.MaterialItemId) x
        WHERE x.q > (SELECT COALESCE(SUM(l.IssuedQuantity),0) FROM StoreRequisitionLines l WHERE l.StoreRequisitionId = x.StoreRequisitionId AND l.MaterialItemId = x.MaterialItemId)
UNION ALL SELECT 'T4 Issue from a WO-linked requisition without WorkOrderId', t.InventoryTransactionId FROM InventoryTransactions t
        JOIN StoreRequisitions s ON s.StoreRequisitionId = t.ReferenceId
        WHERE t.TransactionType = 'Issue' AND t.ReferenceType = 'StoreRequisition' AND s.WorkOrderId IS NOT NULL AND t.WorkOrderId IS NULL
UNION ALL SELECT 'T5 Issue references non-existent WorkOrder', t.InventoryTransactionId FROM InventoryTransactions t
        LEFT JOIN WorkOrders w ON w.WorkOrderId = t.WorkOrderId WHERE t.WorkOrderId IS NOT NULL AND w.WorkOrderId IS NULL
UNION ALL SELECT 'T6 duplicate Issue transactions (same requisition/material/qty/second)', MIN(InventoryTransactionId) FROM InventoryTransactions
        WHERE TransactionType = 'Issue' AND ReferenceType = 'StoreRequisition'
        GROUP BY ReferenceId, MaterialItemId, Quantity, CONVERT(varchar(19), TransactionDate, 120) HAVING COUNT(*) > 1;


-- ===== EVIDENCE (attach this whole output) =====
SELECT @@SERVERNAME AS server_name, DB_NAME() AS database_name, SYSDATETIME() AS executed_at, SUSER_SNAME() AS executed_by;

-- one row per check (24 checks); violations = 0 means clean
SELECT c.chk AS [check], COUNT(v.id) AS violations
FROM (VALUES
    ('S1 stock negative OnHand'),
    ('S2 stock negative Reserved'),
    ('S3 stock Reserved > OnHand'),
    ('R1 line Issued < 0'),
    ('R2 line Approved < 0'),
    ('R3 line Approved > Requested'),
    ('R4 line Issued > COALESCE(Approved,Requested)'),
    ('R5 line Issued > Requested'),
    ('V1 reservation Quantity < 0'),
    ('V2 reservation Issued > Quantity'),
    ('V3 reservation -> requisition missing/inactive'),
    ('V4 reservation -> line missing/inactive'),
    ('V5 reservation line belongs to another requisition'),
    ('V6 reservation material differs from its line'),
    ('V7 duplicate active reservation per line'),
    ('V8 legacy reservation without line (store requisition)'),
    ('V9 legacy reservation AMBIGUOUS (>1 or 0 matching lines)'),
    ('V10 active reservations exceed stock reserved (per stock row)'),
    ('T1 Issue transaction with missing material'),
    ('T2 StoreRequisition Issue with invalid source requisition'),
    ('T3 Issue total per requisition+material exceeds issued on lines'),
    ('T4 Issue from a WO-linked requisition without WorkOrderId'),
    ('T5 Issue references non-existent WorkOrder'),
    ('T6 duplicate Issue transactions (same requisition/material/qty/second)')
) AS c(chk)
LEFT JOIN #viol v ON v.chk = c.chk
GROUP BY c.chk ORDER BY c.chk;

-- affected ids (empty = clean)
SELECT chk AS [check], id AS affected_id FROM #viol ORDER BY chk, id;

-- Constraint trust (target: is_disabled = 0 AND is_not_trusted = 0)
SELECT OBJECT_NAME(parent_object_id) AS tbl, name, is_disabled, is_not_trusted
FROM sys.check_constraints WHERE name IN ('CK_MaterialStocks_Quantities', 'CK_StoreRequisitionLines_Quantities', 'CK_MaterialReservations_Quantities');
