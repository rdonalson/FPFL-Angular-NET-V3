import type * as ExcelJS from 'exceljs';

import { formatDate } from '../../item-detail/schedule';
import { LedgerDay, summarize } from '../display.models';
import { AccountLedger, accountPayoffStatus, describeAccountEvent } from '../accounts/account-ledger.models';
import { DebtPaydown, describeEvent, payoffStatus } from '../paydown/paydown.models';

/**
 * Ledger → Excel, a port of the original FPFL Angular app's ExportService: the same layout as the
 * Ledger tab, one row per day, with each day's credits and debits in an outline group underneath
 * that Excel's +/– buttons expand and collapse. Groups start collapsed, like the ledger on screen;
 * the "2" button at the top left of the sheet expands them all.
 *
 * ExcelJS is about 1 MB, so it is loaded only when someone exports (see exportLedgerToExcel).
 */

type ExcelModule = typeof ExcelJS;

export interface LedgerExportOptions {
  /** 'yyyy-MM-dd' */
  start: string;
  /** 'yyyy-MM-dd' */
  end: string;
  createdAt?: Date;
  /** Tracked debts over the same window; adds a "Debt Paydown" sheet when there are any. */
  paydown?: readonly DebtPaydown[];
  /** Credit cards and loans over the same window; adds a "Cards & Loans" sheet when there are any. */
  accounts?: readonly AccountLedger[];
}

const SHEET = 'Ledger';
const COLUMNS = 5;

const COLOR = {
  header: 'FF1E293B', // the app's top bar
  headerText: 'FFFFFFFF',
  subHeader: 'FFE5E7EB',
  detail: 'FFF5F7FA',
  credit: 'FF15803D',
  debit: 'FFB91C1C',
  muted: 'FF4B5563',
  border: 'FFD1D5DB',
} as const;

/** Accounting style: negatives in red with parentheses, zero as a dash. */
const MONEY = '_($* #,##0.00_);[Red]_($* (#,##0.00);_($* "-"??_);_(@_)';
const BALANCE = '$#,##0.00_);[Red]($#,##0.00)';
const DAY = 'ddd, mmm d, yyyy';

const thin: Partial<ExcelJS.Border> = { style: 'thin', color: { argb: COLOR.border } };
const box: Partial<ExcelJS.Borders> = { top: thin, left: thin, bottom: thin, right: thin };

/** Builds the workbook. Pure apart from the ExcelJS module it is handed, so specs can run it directly. */
export function buildLedgerWorkbook(excel: ExcelModule, days: readonly LedgerDay[], options: LedgerExportOptions): ExcelJS.Workbook {
  const workbook = new excel.Workbook();
  workbook.creator = 'FPFL';
  workbook.created = options.createdAt ?? new Date();

  const ws = workbook.addWorksheet(SHEET, {
    // The +/– button sits on the day row above its items (Excel's default puts it below).
    properties: { outlineLevelRow: 1, outlineProperties: { summaryBelow: false, summaryRight: true } },
  });

  ws.columns = [
    { key: 'date', width: 24 },
    { key: 'credits', width: 16 },
    { key: 'debits', width: 16 },
    { key: 'net', width: 28 },
    { key: 'balance', width: 18 },
  ];

  writeTitle(ws, days, options);
  const headerRow = writeColumnHeaders(ws);
  ws.views = [{ state: 'frozen', ySplit: headerRow, showGridLines: false }];

  for (const day of days) {
    writeDay(ws, day);
    if (day.items.length > 0) {
      writeItems(ws, day);
    }
  }

  if (options.paydown?.length) {
    writePaydownSheet(workbook, options.paydown, options);
  }
  if (options.accounts?.length) {
    writeAccountsSheet(workbook, options.accounts, options);
  }

  return workbook;
}

/** Loads ExcelJS, builds the workbook and hands the browser the file. */
export async function exportLedgerToExcel(days: readonly LedgerDay[], options: LedgerExportOptions): Promise<void> {
  const excel = resolveExcel(await import('exceljs'));
  const workbook = buildLedgerWorkbook(excel, days, options);
  const buffer = await workbook.xlsx.writeBuffer();
  const blob = new Blob([buffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });

  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = ledgerFileName(options);
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 0);
}

/** ExcelJS ships as CommonJS; depending on the bundler its exports arrive directly or under `default`. */
export function resolveExcel(module: unknown): ExcelModule {
  return ((module as { default?: ExcelModule }).default ?? module) as ExcelModule;
}

export function ledgerFileName(options: Pick<LedgerExportOptions, 'start' | 'end'>): string {
  return `Forecast Ledger ${options.start} to ${options.end}.xlsx`;
}

/** 'yyyy-MM-dd' → a Date Excel shows as that same calendar day in any time zone (ExcelJS writes UTC). */
export function excelDate(iso: string): Date {
  const [year, month, day] = iso.split('-').map(Number);
  return new Date(Date.UTC(year, month - 1, day));
}

function writeTitle(ws: ExcelJS.Worksheet, days: readonly LedgerDay[], options: LedgerExportOptions): void {
  const title = ws.addRow(['Forecast Ledger']);
  title.font = { size: 16, bold: true };
  title.height = 24;

  const range = ws.addRow([`${formatDate(options.start)} – ${formatDate(options.end)} · ${days.length} ${days.length === 1 ? 'day' : 'days'}`]);
  range.font = { color: { argb: COLOR.muted } };

  const created = options.createdAt ?? new Date();
  const createdRow = ws.addRow([`Created ${created.toLocaleString('en-US', { dateStyle: 'medium', timeStyle: 'short' })}`]);
  createdRow.font = { italic: true, color: { argb: COLOR.muted } };

  for (const row of [title, range, createdRow]) {
    ws.mergeCells(row.number, 1, row.number, COLUMNS);
  }

  const summary = summarize(days);
  if (!summary) {
    ws.addRow([]);
    return;
  }

  ws.addRow([]);
  const lines: [string, number, string, string?][] = [
    ['Starting balance', summary.startingBalance, BALANCE],
    ['Credits', summary.totalCredits, MONEY, COLOR.credit],
    ['Debits', summary.totalDebits, MONEY, COLOR.debit],
    ['Ending balance', summary.endingBalance, BALANCE],
    ['Lowest balance', summary.lowest.amount, BALANCE],
  ];
  for (const [label, value, format, color] of lines) {
    const row = ws.addRow([label, value]);
    row.getCell(1).font = { bold: true };
    row.getCell(2).numFmt = format;
    if (color) {
      row.getCell(2).font = { color: { argb: color } };
    }
    if (label === 'Lowest balance') {
      row.getCell(3).value = `on ${formatDate(summary.lowest.date)}`;
      row.getCell(3).font = { color: { argb: COLOR.muted } };
    }
  }
  if (summary.firstNegativeDate) {
    const warning = ws.addRow([`Your balance drops below $0.00 on ${formatDate(summary.firstNegativeDate)}.`]);
    warning.font = { bold: true, color: { argb: COLOR.debit } };
    ws.mergeCells(warning.number, 1, warning.number, COLUMNS);
  }
  ws.addRow([]);
}

/** @returns the header's row number, for freezing the panes under it. */
function writeColumnHeaders(ws: ExcelJS.Worksheet): number {
  const header = ws.addRow(['Date', 'Credits', 'Debits', 'Net change', 'Balance']);
  header.height = 20;
  header.eachCell((cell, column) => {
    cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: COLOR.header } };
    cell.font = { bold: true, color: { argb: COLOR.headerText } };
    cell.border = box;
    cell.alignment = { vertical: 'middle', horizontal: column === 1 ? 'left' : 'right' };
  });
  return header.number;
}

function writeDay(ws: ExcelJS.Worksheet, day: LedgerDay): void {
  const row = ws.addRow([excelDate(day.wDate), day.creditSummary, day.debitSummary, day.net, day.runningTotal]);
  row.getCell(1).numFmt = DAY;
  row.getCell(1).alignment = { horizontal: 'left' };
  for (const column of [2, 3, 4]) {
    row.getCell(column).numFmt = MONEY;
  }
  row.getCell(5).numFmt = BALANCE;

  row.getCell(2).font = { color: { argb: day.creditSummary ? COLOR.credit : COLOR.muted } };
  row.getCell(3).font = { color: { argb: day.debitSummary ? COLOR.debit : COLOR.muted } };
  row.getCell(4).font = { color: { argb: day.net > 0 ? COLOR.credit : day.net < 0 ? COLOR.debit : COLOR.muted } };
  row.getCell(5).font = { bold: true };

  if (day.items.length > 0) {
    row.getCell(1).font = { bold: true };
  }
  row.eachCell({ includeEmpty: true }, (cell) => (cell.border = { bottom: thin }));
}

/** The day's items as a collapsed outline group: a small header, then one row per credit or debit. */
function writeItems(ws: ExcelJS.Worksheet, day: LedgerDay): void {
  const header = ws.addRow([null, 'Credits', 'Debits', 'Name', 'Period']);
  header.outlineLevel = 1;
  header.hidden = true;
  header.eachCell((cell, column) => {
    if (column === 1) {
      return; // indent
    }
    cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: COLOR.subHeader } };
    cell.font = { bold: true, size: 10 };
    cell.border = box;
    cell.alignment = { horizontal: column <= 3 ? 'right' : 'left' };
  });

  for (const item of day.items) {
    const credit = item.amount > 0;
    const row = ws.addRow([null, credit ? item.amount : null, credit ? null : item.amount, item.name ?? '', item.period ?? '']);
    row.outlineLevel = 1;
    row.hidden = true;
    for (let column = 2; column <= COLUMNS; column++) {
      const cell = row.getCell(column);
      cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: COLOR.detail } };
      cell.border = box;
    }
    row.getCell(2).numFmt = MONEY;
    row.getCell(3).numFmt = MONEY;
    row.getCell(2).font = { color: { argb: COLOR.credit } };
    row.getCell(3).font = { color: { argb: COLOR.debit } };
  }
}

const PAYDOWN_SHEET = 'Debt Paydown';
const PAYDOWN_COLUMNS = 6;

/**
 * One summary row per debt (owed at the start, paid, interest, owed at the end, payoff), with its
 * payments, allocations and interest in a collapsed outline group underneath, like the Ledger sheet.
 */
function writePaydownSheet(workbook: ExcelJS.Workbook, debts: readonly DebtPaydown[], options: LedgerExportOptions): void {
  const ws = workbook.addWorksheet(PAYDOWN_SHEET, {
    properties: { outlineLevelRow: 1, outlineProperties: { summaryBelow: false, summaryRight: true } },
  });
  ws.columns = [{ width: 26 }, { width: 20 }, { width: 24 }, { width: 16 }, { width: 18 }, { width: 22 }];

  const title = ws.addRow(['Debt Paydown']);
  title.font = { size: 16, bold: true };
  title.height = 24;
  const range = ws.addRow([`${formatDate(options.start)} – ${formatDate(options.end)}`]);
  range.font = { color: { argb: COLOR.muted } };
  for (const row of [title, range]) {
    ws.mergeCells(row.number, 1, row.number, PAYDOWN_COLUMNS);
  }
  ws.addRow([]);

  const header = ws.addRow(['Debt', 'Owed at start', 'Paid', 'Interest', 'Owed at end', 'Payoff']);
  header.height = 20;
  header.eachCell((cell, column) => {
    cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: COLOR.header } };
    cell.font = { bold: true, color: { argb: COLOR.headerText } };
    cell.border = box;
    cell.alignment = { vertical: 'middle', horizontal: column === 1 || column === PAYDOWN_COLUMNS ? 'left' : 'right' };
  });
  ws.views = [{ state: 'frozen', ySplit: header.number, showGridLines: false }];

  for (const debt of debts) {
    const status = payoffStatus(debt, options.end);
    const payoff =
      status === 'never' ? 'Not within 30 years' : `${formatDate(debt.payoffDate)}${status === 'later' ? ' (after range)' : ''}`;

    const row = ws.addRow([debt.name ?? 'Debt', debt.startingBalance, debt.totalPaid, debt.totalInterest, debt.endingBalance, payoff]);
    row.getCell(1).font = { bold: true };
    for (const column of [2, 3, 4, 5]) {
      row.getCell(column).numFmt = column === 3 || column === 4 ? MONEY : BALANCE;
    }
    row.getCell(3).font = { color: { argb: COLOR.credit } };
    row.getCell(4).font = { color: { argb: debt.totalInterest ? COLOR.debit : COLOR.muted } };
    row.getCell(5).font = { bold: true };
    row.getCell(6).font = { color: { argb: status === 'never' ? COLOR.debit : COLOR.credit } };
    row.eachCell({ includeEmpty: true }, (cell) => (cell.border = { bottom: thin }));

    if (debt.events.length > 0) {
      const sub = ws.addRow([null, 'Date', 'Activity', 'Amount', 'Balance left']);
      sub.outlineLevel = 1;
      sub.hidden = true;
      sub.eachCell((cell, column) => {
        if (column === 1) {
          return;
        }
        cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: COLOR.subHeader } };
        cell.font = { bold: true, size: 10 };
        cell.border = box;
        cell.alignment = { horizontal: column >= 4 ? 'right' : 'left' };
      });

      for (const event of debt.events) {
        const line = ws.addRow([null, excelDate(event.date), describeEvent(event), Math.abs(event.amount), event.balanceAfter]);
        line.outlineLevel = 1;
        line.hidden = true;
        for (let column = 2; column <= 5; column++) {
          const cell = line.getCell(column);
          cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: COLOR.detail } };
          cell.border = box;
        }
        line.getCell(2).numFmt = DAY;
        line.getCell(2).alignment = { horizontal: 'left' };
        line.getCell(4).numFmt = MONEY;
        line.getCell(4).font = { color: { argb: event.kind === 'interest' ? COLOR.debit : COLOR.credit } };
        line.getCell(5).numFmt = BALANCE;
      }
    }
  }
}

const ACCOUNTS_SHEET = 'Cards & Loans';
const ACCOUNTS_COLUMNS = 7;

/**
 * One summary row per card or loan (owed at the start, purchases, paid, interest, owed at the end, payoff),
 * with its purchases, payments and interest in a collapsed outline group underneath, like the other sheets.
 */
function writeAccountsSheet(workbook: ExcelJS.Workbook, accounts: readonly AccountLedger[], options: LedgerExportOptions): void {
  const ws = workbook.addWorksheet(ACCOUNTS_SHEET, {
    properties: { outlineLevelRow: 1, outlineProperties: { summaryBelow: false, summaryRight: true } },
  });
  ws.columns = [{ width: 26 }, { width: 20 }, { width: 26 }, { width: 16 }, { width: 16 }, { width: 18 }, { width: 22 }];

  const title = ws.addRow(['Cards & Loans']);
  title.font = { size: 16, bold: true };
  title.height = 24;
  const range = ws.addRow([`${formatDate(options.start)} – ${formatDate(options.end)}`]);
  range.font = { color: { argb: COLOR.muted } };
  for (const row of [title, range]) {
    ws.mergeCells(row.number, 1, row.number, ACCOUNTS_COLUMNS);
  }
  ws.addRow([]);

  const header = ws.addRow(['Card / loan', 'Owed at start', 'Purchases', 'Paid', 'Interest', 'Owed at end', 'Payoff']);
  header.height = 20;
  header.eachCell((cell, column) => {
    cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: COLOR.header } };
    cell.font = { bold: true, color: { argb: COLOR.headerText } };
    cell.border = box;
    cell.alignment = { vertical: 'middle', horizontal: column === 1 || column === ACCOUNTS_COLUMNS ? 'left' : 'right' };
  });
  ws.views = [{ state: 'frozen', ySplit: header.number, showGridLines: false }];

  for (const account of accounts) {
    const status = accountPayoffStatus(account, options.end);
    const payoff =
      status === 'never' ? 'Not within 30 years' : `${formatDate(account.payoffDate)}${status === 'later' ? ' (after range)' : ''}`;

    const row = ws.addRow([
      account.name,
      account.startingBalance,
      account.totalPurchases,
      account.totalPaid,
      account.totalInterest,
      account.endingBalance,
      payoff,
    ]);
    row.getCell(1).font = { bold: true };
    for (const column of [2, 3, 4, 5, 6]) {
      row.getCell(column).numFmt = column === 2 || column === 6 ? BALANCE : MONEY;
    }
    row.getCell(3).font = { color: { argb: account.totalPurchases ? COLOR.debit : COLOR.muted } };
    row.getCell(4).font = { color: { argb: COLOR.credit } };
    row.getCell(5).font = { color: { argb: account.totalInterest ? COLOR.debit : COLOR.muted } };
    row.getCell(6).font = { bold: true };
    row.getCell(7).font = { color: { argb: status === 'never' ? COLOR.debit : COLOR.credit } };
    row.eachCell({ includeEmpty: true }, (cell) => (cell.border = { bottom: thin }));

    if (account.events.length > 0) {
      const sub = ws.addRow([null, 'Date', 'Activity', 'Amount', 'Balance after']);
      sub.outlineLevel = 1;
      sub.hidden = true;
      sub.eachCell((cell, column) => {
        if (column === 1) {
          return;
        }
        cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: COLOR.subHeader } };
        cell.font = { bold: true, size: 10 };
        cell.border = box;
        cell.alignment = { horizontal: column >= 4 ? 'right' : 'left' };
      });

      for (const event of account.events) {
        // Signed like the balance: purchases and interest add, payments subtract.
        const line = ws.addRow([null, excelDate(event.date), describeAccountEvent(event), event.amount, event.balanceAfter]);
        line.outlineLevel = 1;
        line.hidden = true;
        for (let column = 2; column <= 5; column++) {
          const cell = line.getCell(column);
          cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: COLOR.detail } };
          cell.border = box;
        }
        line.getCell(2).numFmt = DAY;
        line.getCell(2).alignment = { horizontal: 'left' };
        line.getCell(4).numFmt = MONEY;
        line.getCell(4).font = { color: { argb: event.kind === 'payment' ? COLOR.credit : COLOR.debit } };
        line.getCell(5).numFmt = BALANCE;
      }
    }
  }
}
