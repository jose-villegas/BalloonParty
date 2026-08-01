#!/usr/bin/env node
// Validates every ```mermaid block in the repo's Markdown by rendering it with mermaid-cli.
// Nothing else catches a broken diagram: the style audit is C#-only, and both render targets
// (GitHub + the Doxygen site) fail silently, showing a parse error where the diagram should be.
//
//   node Tools/validate-mermaid.mjs                 # scan the default roots
//   node Tools/validate-mermaid.mjs path/to/file.md # scan specific files or directories
//   node Tools/validate-mermaid.mjs --verbose       # also list the blocks that passed
//
// The skill's vendored `references/` are upstream's docs, not ours — scan them explicitly
// if you want them checked.
//
// Requires: npm install -g @mermaid-js/mermaid-cli

import { execFile } from 'node:child_process';
import { mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { readdirSync, statSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, relative, resolve } from 'node:path';
import { promisify } from 'node:util';

const execFileAsync = promisify(execFile);

const DEFAULT_ROOTS = ['Assets', 'README.md', '.claude/agents', '.claude/skills/mermaid/SKILL.md'];
const CONCURRENCY = 4;
const FENCE = /^[ \t]*```+[ \t]*mermaid[ \t]*$/;
const CLOSING_FENCE = /^[ \t]*```+[ \t]*$/;

function collectMarkdown(target, out) {
  let stat;
  try {
    stat = statSync(target);
  } catch {
    return out;
  }

  if (stat.isFile()) {
    if (target.endsWith('.md')) {
      out.push(target);
    }
    return out;
  }

  for (const entry of readdirSync(target, { withFileTypes: true })) {
    // Library and generated trees have their own diagrams we neither own nor fix.
    if (entry.isDirectory() && ['node_modules', '.git', 'Library', 'Temp', 'Docs'].includes(entry.name)) {
      continue;
    }
    collectMarkdown(join(target, entry.name), out);
  }

  return out;
}

async function extractBlocks(file) {
  const lines = (await readFile(file, 'utf8')).split(/\r?\n/);
  const blocks = [];
  let start = -1;
  let body = [];

  for (let i = 0; i < lines.length; i++) {
    if (start === -1) {
      if (FENCE.test(lines[i])) {
        start = i + 1;
        body = [];
      }
      continue;
    }

    if (CLOSING_FENCE.test(lines[i])) {
      blocks.push({ line: start, source: body.join('\n') });
      start = -1;
      continue;
    }

    body.push(lines[i]);
  }

  if (start !== -1) {
    blocks.push({ line: start, source: body.join('\n'), unterminated: true });
  }

  return blocks;
}

// mermaid-cli reports the line within the block; map it back to the file so the
// output is clickable.
function rewriteLineNumbers(message, blockStartLine) {
  return message.replace(/(Parse|Lexical) error on line (\d+)/g, (_, kind, n) =>
    `${kind} error on file line ${Number(n) + blockStartLine - 1} (line ${n} of the block)`);
}

async function validate(block, file, workDir, index) {
  const label = `${relative(process.cwd(), file)}:${block.line}`;

  if (block.unterminated) {
    return { label, ok: false, message: 'Unterminated ```mermaid fence — no closing ```' };
  }

  if (!block.source.trim()) {
    return { label, ok: false, message: 'Empty mermaid block' };
  }

  const input = join(workDir, `block-${index}.mmd`);
  const output = join(workDir, `block-${index}.svg`);
  await writeFile(input, block.source, 'utf8');

  try {
    await execFileAsync('mmdc', ['-i', input, '-o', output, '-q'], {
      shell: process.platform === 'win32',
      maxBuffer: 1024 * 1024 * 10,
    });
    return { label, ok: true };
  } catch (error) {
    const raw = `${error.stderr || ''}${error.stdout || ''}`.trim() || error.message;
    const parseError = raw.match(/Error:[\s\S]*?(?=\n\s*at\s|$)/);
    const message = rewriteLineNumbers((parseError ? parseError[0] : raw).trim(), block.line);
    return { label, ok: false, message };
  }
}

async function main() {
  const args = process.argv.slice(2);
  const verbose = args.includes('--verbose');
  const targets = args.filter((a) => a !== '--verbose');
  const roots = (targets.length ? targets : DEFAULT_ROOTS).map((p) => resolve(process.cwd(), p));

  const files = [];
  for (const root of roots) {
    collectMarkdown(root, files);
  }

  const tasks = [];
  for (const file of files.sort()) {
    for (const block of await extractBlocks(file)) {
      tasks.push({ file, block });
    }
  }

  if (!tasks.length) {
    console.log('No mermaid blocks found.');
    return;
  }

  // Probe once, so a missing CLI reads as one actionable line instead of N identical
  // per-block failures.
  try {
    await execFileAsync('mmdc', ['--version'], { shell: process.platform === 'win32' });
  } catch {
    console.error('mermaid-cli (mmdc) not found — cannot validate diagrams.');
    console.error('Install it with:  npm install -g @mermaid-js/mermaid-cli');
    process.exitCode = 1;
    return;
  }

  console.log(`Validating ${tasks.length} mermaid block(s)...\n`);

  const results = new Array(tasks.length);
  const workDir = await mkdtemp(join(tmpdir(), 'mermaid-validate-'));
  let cursor = 0;

  try {
    await Promise.all(
      Array.from({ length: Math.min(CONCURRENCY, tasks.length) }, async () => {
        while (cursor < tasks.length) {
          const i = cursor++;
          const { file, block } = tasks[i];
          results[i] = await validate(block, file, workDir, i);
        }
      }),
    );
  } finally {
    await rm(workDir, { recursive: true, force: true });
  }

  const failures = results.filter((r) => !r.ok);

  if (verbose) {
    for (const result of results) {
      if (result.ok) {
        console.log(`  PASS  ${result.label}`);
      }
    }
  }

  if (failures.length) {
    console.log('');
    for (const failure of failures) {
      console.log(`  FAIL  ${failure.label}`);
      for (const line of failure.message.split('\n')) {
        console.log(`        ${line}`);
      }
      console.log('');
    }
  }

  console.log(`\n${results.length - failures.length} passed, ${failures.length} failed.`);
  process.exitCode = failures.length ? 1 : 0;
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
