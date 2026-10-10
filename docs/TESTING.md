# Test plan

## 0. Automated logic tests (already passing in the sandbox)
    cd tests/MyActivity.LogicTests && dotnet run        # expect: ALL 67 CHECKS PASSED

## 1. Accounts and security
1.  Sign up with empty fields -> each message appears. Sign up OK.
2.  Same Employee ID again -> "Employee ID already exists."  Same email again -> duplicate message.
3.  Wrong password 5 times -> "Too many failed attempts. Try again in N seconds."  Right password after the wait works.
4.  "Keep me logged in" on -> force-close app -> reopens on Home.  Off -> reopens on Login.
5.  More > Change password: wrong current password rejected; new password works on next login.
6.  Create a 2nd account. Add data in each. Confirm neither sees the other's attendance, transactions, meetings, tasks.
7.  Log out: reminders of that user stop (check by waiting for a scheduled one).

## 2. Attendance
1.  More > (Attendance tab > Settings): set check-in 2 min ahead, save. Alert arrives with Present / Absent / Next 5 min.
2.  Next 5 min -> nothing recorded, same alert after exactly 5 minutes; repeat twice.
3.  Present -> Home shows Present; no more alerts for that event.  Absent for check-out -> shows Absent.
4.  Close the app completely, press a button on the next alert (Android) -> record is saved. (If not, see PLATFORM_SETUP.)
5.  History: Today / This week / This month / Custom.

## 3. Account
1.  Add credit 10,000 and debit 4,500 -> balance 5,500.00. Home shows the same.
2.  Edit a transaction amount -> totals update. Delete -> confirmation, totals update.
3.  Amount 0, 1.234, blank, text -> clear error each time.
4.  History filters: type, category, this month, custom range; summary matches the list.

## 4. Calculators (compare with a bank/online calculator)
- EMI 10,00,000 @ 8.5% 20 yr  -> EMI 8,678.23
- SIP 5,000/month @ 12% 10 yr -> maturity 11,61,695.38 (invested 6,00,000)
- SWP 10,00,000 @ 8%, 8,000/month, 10 yr -> withdrawn 9,60,000, remaining about 7,56,072
- Basic: 200 + 10% = 220; 5 / 0 -> "Cannot divide by zero"; 0.1 + 0.2 = 0.3

## 5. Meetings and tasks
1.  Meeting today +15 min, reminder 10 min -> notification "Project ... starts in 10 minutes." Tap opens its details.
2.  Edit the time -> only ONE reminder fires (old one cancelled). Delete -> no reminder.
3.  Reminder time already passed -> saved with a message that no reminder was set.
4.  Task with reminder +2 min -> notification "Task Reminder"; Mark complete / Snooze 10 min buttons work.
5.  Complete a task in the app before its reminder -> no notification.
6.  Dashboard shows the next meeting and next task.

## 6. Offline and restart
1.  Airplane mode ON: repeat sections 1-5. Everything works.
2.  Restart the phone with future reminders -> they still fire (Android; check battery settings on Xiaomi/Oppo/Vivo).
3.  Deny notification permission: app works, banners offer to turn it on.

## 7. UI
Light and dark theme (More > Appearance), small phone, large font size, rotation, empty states, loading spinner.
