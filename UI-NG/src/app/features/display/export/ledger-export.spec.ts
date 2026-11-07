import * as ExcelModule from 'exceljs';
import type { Row, Worksheet } from 'exceljs';

import { makeDay, sampleAccount, sampleLedger, samplePaydown } from '../testing-ledger';
import { buildLedgerWorkbook, excelDate, ledgerFileName, resolveExcel } from './ledger-export';

describe('ledger export', () => {
  const excel = resolveExcel(ExcelModule);
  const options = { start: '2026-10-17', end: '2026-10-19', createdAt: new Date(2026, 9, 17, 19, 30) };

  function sheet(days = sampleLedger()): Worksheet {
    return buildLedgerWorkbook(excel, days, options).getWorksheet('Ledger')!;
  }

  /** Rows whose first cell is a date, i.e. the day rows. */
  function dayRows(ws: Worksheet): Row[] {
    const rows: Row[] = [];
    ws.eachRow((row) => {
      if (row.getCell(1).value instanceof Date) {
        rows.push(row);
      }
    });
    return rows;
  }

  /** Values of columns A–E (exceljs's row.values skips empty cells). */
  function cells(row: Row): unknown[] {
    return [1, 2, 3, 4, 5].map((column) => row.getCell(column).value);
  }

  function rowAfter(ws: Worksheet, row: Row, offset: number): Row {
    return ws.getRow(row.number + offset);
  }

  it('starts with a title, the range and a summary', () => {
    const ws = sheet();

    expect(ws.getCell('A1').value).toBe('Forecast Ledger');
    expect(ws.getCell('A2').value).toBe('Oct 17, 2026 – Oct 19, 2026 · 3 days');
    expect(String(ws.getCell('A3').value)).toContain('Created Oct 17, 2026');

    const labels: unknown[] = [];
    ws.eachRow((row) => labels.push(row.getCell(1).value));
    expect(labels).toContain('Starting balance');
    expect(labels).toContain('Ending balance');
  });

  it('writes one row per day under the column headers', () => {
    const ws = sheet();
    const days = dayRows(ws);

    expect(days).toHaveLength(3);
    const header = ws.getRow(days[0].number - 1);
    expect(cells(header)).toEqual(['Date', 'Credits', 'Debits', 'Net change', 'Balance']);

    const [date, credits, debits, net, balance] = cells(days[1]);
    expect(date).toEqual(excelDate('2026-10-18'));
    expect([credits, debits, net, balance]).toEqual([2500, -2200, 300, 1300]);
  });

  it('puts a day’s items in a collapsed outline group below it', () => {
    const ws = sheet();
    const [quiet, busy, next] = dayRows(ws);

    expect(ws.properties.outlineProperties?.summaryBelow).toBe(false);

    // A day without items is followed straight away by the next day.
    expect(busy.number).toBe(quiet.number + 1);

    const subHeader = rowAfter(ws, busy, 1);
    expect(cells(subHeader)).toEqual([null, 'Credits', 'Debits', 'Name', 'Period']);

    const paycheck = rowAfter(ws, busy, 2);
    const rent = rowAfter(ws, busy, 3);
    expect(cells(paycheck)).toEqual([null, 2500, null, 'Paycheck', 'Every Two Weeks']);
    expect(cells(rent)).toEqual([null, null, -2200, 'Rent', 'Monthly']);

    for (const row of [subHeader, paycheck, rent]) {
      expect(row.outlineLevel).toBe(1);
      expect(row.hidden).toBe(true);
    }
    expect(busy.outlineLevel ?? 0).toBe(0);
    expect(next.number).toBe(busy.number + 4);
  });

  it('warns in the sheet when the balance goes negative', () => {
    const ws = sheet([makeDay(1, '2026-10-17', 100), makeDay(2, '2026-10-18', -50, [{ amount: -150 }])]);

    const text: unknown[] = [];
    ws.eachRow((row) => text.push(row.getCell(1).value));
    expect(text).toContain('Your balance drops below $0.00 on Oct 18, 2026.');
  });

  it('produces a real .xlsx file', async () => {
    const buffer = await buildLedgerWorkbook(excel, sampleLedger(), options).xlsx.writeBuffer();
    const bytes = new Uint8Array(buffer as ArrayBuffer);

    expect(bytes.length).toBeGreaterThan(1000);
    expect(String.fromCharCode(bytes[0], bytes[1])).toBe('PK'); // .xlsx is a zip
  });

  it('keeps calendar days in any time zone and names the file by range', () => {
    expect(excelDate('2026-10-18').toISOString()).toBe('2026-10-18T00:00:00.000Z');
    expect(ledgerFileName(options)).toBe('Forecast Ledger 2026-10-17 to 2026-10-19.xlsx');
  });

  it('adds a Debt Paydown sheet with each debt and its activity grouped underneath', () => {
    const workbook = buildLedgerWorkbook(excel, sampleLedger(), { ...options, paydown: [samplePaydown()] });
    const ws = workbook.getWorksheet('Debt Paydown')!;

    expect(ws.getCell('A1').value).toBe('Debt Paydown');

    let summary: Row | undefined;
    ws.eachRow((row) => {
      if (row.getCell(1).value === 'Chase Visa') {
        summary = row;
      }
    });
    expect(cells(summary!)).toEqual(['Chase Visa', 1000, 825, 0, 175]);
    expect(summary!.getCell(6).value).toBe('Nov 18, 2026 (after range)');

    const sub = ws.getRow(summary!.number + 1);
    const payment = ws.getRow(summary!.number + 2);
    const allocation = ws.getRow(summary!.number + 3);
    expect(cells(sub)).toEqual([null, 'Date', 'Activity', 'Amount', 'Balance left']);
    expect(cells(payment)).toEqual([null, excelDate('2026-10-18'), 'Scheduled payment', 200, 800]);
    expect(cells(allocation)).toEqual([null, excelDate('2026-10-18'), '25% of Paycheck', 625, 175]);
    for (const row of [sub, payment, allocation]) {
      expect(row.outlineLevel).toBe(1);
      expect(row.hidden).toBe(true);
    }
  });

  it('leaves the Debt Paydown sheet out when nothing is tracked', () => {
    const workbook = buildLedgerWorkbook(excel, sampleLedger(), { ...options, paydown: [] });
    expect(workbook.getWorksheet('Debt Paydown')).toBeUndefined();
  });

  it('adds a Cards & Loans sheet only when there are accounts', () => {
    expect(buildLedgerWorkbook(excel, sampleLedger(), options).getWorksheet('Cards & Loans')).toBeUndefined();

    const ws = buildLedgerWorkbook(excel, sampleLedger(), { ...options, accounts: [sampleAccount()] }).getWorksheet('Cards & Loans')!;
    const summary: unknown[] = [];
    ws.eachRow((row) => {
      if (row.getCell(1).value === 'Chase Visa') {
        summary.push(...[1, 2, 3, 4, 5, 6, 7].map((c) => row.getCell(c).value));
      }
    });
    expect(summary).toEqual(['Chase Visa', 1000, 50, 625, 0, 425, 'Nov 1, 2026 (after range)']);
  });
});
