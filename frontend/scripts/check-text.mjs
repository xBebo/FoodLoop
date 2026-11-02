import { readdir, readFile } from 'node:fs/promises'
import { extname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = fileURLToPath(new URL('../', import.meta.url))
const decoder = new TextDecoder('utf-8', { fatal: true })
const extensions = new Set(['.ts', '.tsx', '.js', '.mjs', '.css', '.html', '.json'])
// Common UTF-8 bytes accidentally decoded as Windows-1252, plus replacement characters.
const corrupted = /[\uFFFD]|\u00C2[\u0080-\u00BF]|\u00E2[\u0080-\u00BF\u2000-\u2122]|\u00F0\u0178/
let failures = 0
async function check(path) {
  let text
  try { text = decoder.decode(await readFile(path)) } catch {
    console.error(`${path}: invalid UTF-8`)
    failures++
    return
  }
  text.split(/\r?\n/).forEach((line, index) => {
    if (corrupted.test(line)) {
      console.error(`${path}:${index + 1}: possible corrupted text`)
      failures++
    }
  })
}
async function walk(dir) {
  for (const entry of await readdir(dir, { withFileTypes: true })) {
    const path = join(dir, entry.name)
    if (entry.isDirectory()) await walk(path)
    else if (extensions.has(extname(path))) await check(path)
  }
}
await walk(join(root, 'src'))
await check(join(root, 'index.html'))
if (failures) process.exitCode = 1
else console.log('Frontend text: valid UTF-8; no known corruption patterns.')
