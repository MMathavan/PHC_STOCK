-- =====================================================
-- CUSTOM MENU SETUP SCRIPT - ADD ONLY (NO DELETION)
-- Adds: Stock (with Closing), Reports (with Manual Entry)
-- NOTE: Existing menus (Transaction, Tally, SMS) remain unchanged
-- =====================================================

-- =====================================================
-- STEP 1: ADD STOCK MENU (Parent) + CLOSING (Child)
-- =====================================================
-- Parent Menu: Stock
-- Note: Use a unique MenuGId for Stock group (e.g., 100)
INSERT INTO MenuRoleMaster (LinkText, ActionName, ControllerName, Roles, MenuGId, MenuGIndex)
VALUES 
    ('Stock', 'Index', 'Stock', 'admin', 100, 1),
    ('Stock', 'Index', 'Stock', 'user', 100, 1);

-- Child Menu: Closing (under Stock group)
INSERT INTO MenuRoleMaster (LinkText, ActionName, ControllerName, Roles, MenuGId, MenuGIndex)
VALUES 
    ('Closing', 'Closing', 'Stock', 'admin', 100, 2),
    ('Closing', 'Closing', 'Stock', 'user', 100, 2);

PRINT 'Step 1: Added Stock menu with Closing submenu';

-- =====================================================
-- STEP 2: ADD REPORTS MENU (Parent) + MANUAL ENTRY (Child)
-- =====================================================
-- Parent Menu: Reports
-- Note: Use a unique MenuGId for Reports group (e.g., 200)
INSERT INTO MenuRoleMaster (LinkText, ActionName, ControllerName, Roles, MenuGId, MenuGIndex)
VALUES 
    ('Reports', 'Index', 'Reports', 'admin', 200, 1),
    ('Reports', 'Index', 'Reports', 'user', 200, 1);

-- Child Menu: Manual Entry (under Reports group)
INSERT INTO MenuRoleMaster (LinkText, ActionName, ControllerName, Roles, MenuGId, MenuGIndex)
VALUES 
    ('Manual Entry', 'ManualEntry', 'Reports', 'admin', 200, 2),
    ('Manual Entry', 'ManualEntry', 'Reports', 'user', 200, 2);

PRINT 'Step 3: Added Reports menu with Manual Entry submenu';

-- =====================================================
-- VERIFICATION QUERY
-- =====================================================
SELECT * FROM MenuRoleMaster 
ORDER BY MenuGId, MenuGIndex;

PRINT 'Menu setup complete! Existing menus NOT deleted.';
