import { existsSync } from 'node:fs'
import { spawn } from 'node:child_process'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const frontendRoot = dirname(fileURLToPath(import.meta.url))
const viteCli = join(frontendRoot, 'node_modules', 'vite', 'bin', 'vite.js')

if (!existsSync(viteCli)) {
  const npm = resolveNpmCommand()
  await run(npm.command, [...npm.args, 'ci'], frontendRoot)
}

await run(process.execPath, [viteCli, ...process.argv.slice(2)], frontendRoot, true)

function run(command, args, cwd, inherit = false) {
  return new Promise((resolve, reject) => {
    const env = withCommandDirectoryOnPath(command)
    const child = spawn(command, args, {
      cwd,
      shell: process.platform === 'win32' && command === 'npm',
      stdio: inherit ? 'inherit' : ['ignore', 'inherit', 'inherit'],
      env,
    })

    child.on('error', reject)
    child.on('exit', (code) => {
      if (code === 0) {
        resolve()
      } else {
        reject(new Error(`${command} ${args.join(' ')} exited with code ${code}`))
      }
    })
  })
}

function resolveNpmCommand() {
  if (process.platform === 'win32') {
    const programFiles = process.env.ProgramFiles
    if (programFiles) {
      const npmCli = join(programFiles, 'nodejs', 'node_modules', 'npm', 'bin', 'npm-cli.js')
      if (existsSync(npmCli)) return { command: process.execPath, args: [npmCli] }
    }
  }

  return { command: 'npm', args: [] }
}

function withCommandDirectoryOnPath(command) {
  const env = { ...process.env }
  if (process.platform !== 'win32' || command === 'npm') return env
  const separator = ';'
  const windowsSystemPath = [
    process.env.SystemRoot ? join(process.env.SystemRoot, 'system32') : 'C:\\Windows\\system32',
    process.env.SystemRoot ?? 'C:\\Windows',
    process.env.SystemRoot ? join(process.env.SystemRoot, 'System32', 'Wbem') : 'C:\\Windows\\System32\\Wbem',
  ].join(separator)
  const path = `${dirname(command)}${separator}${windowsSystemPath}`
  for (const key of Object.keys(env)) {
    if (key.toLowerCase() === 'path') delete env[key]
  }
  env.PATH = path
  env.Path = path
  return env
}
