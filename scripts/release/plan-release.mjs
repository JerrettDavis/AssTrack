import { execFileSync } from 'node:child_process'
import { mkdirSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import process from 'node:process'
import semver from 'semver'

const defaultNotesPath = path.resolve('artifacts', 'release', 'release-notes.md')
const notesPath = process.env.RELEASE_NOTES_PATH
  ? path.resolve(process.env.RELEASE_NOTES_PATH)
  : defaultNotesPath

const conventionalTypeMap = new Map([
  ['feat', 'features'],
  ['fix', 'fixes'],
  ['perf', 'performance'],
  ['docs', 'docs'],
  ['build', 'maintenance'],
  ['ci', 'maintenance'],
  ['chore', 'maintenance'],
  ['deps', 'maintenance'],
  ['refactor', 'maintenance'],
  ['revert', 'maintenance'],
  ['test', 'maintenance'],
])

const fallbackPrefixes = [
  ['add ', 'features'],
  ['introduce ', 'features'],
  ['implement ', 'features'],
  ['create ', 'features'],
  ['fix ', 'fixes'],
  ['correct ', 'fixes'],
  ['resolve ', 'fixes'],
  ['perf ', 'performance'],
  ['optimize ', 'performance'],
  ['document ', 'docs'],
  ['docs ', 'docs'],
  ['update ', 'maintenance'],
  ['upgrade ', 'maintenance'],
  ['bump ', 'maintenance'],
  ['refine ', 'maintenance'],
  ['move ', 'maintenance'],
  ['organize ', 'maintenance'],
  ['paginate ', 'maintenance'],
  ['disable ', 'maintenance'],
  ['animate ', 'maintenance'],
  ['clamp ', 'maintenance'],
]

function git(...args) {
  return execFileSync('git', args, {
    cwd: process.cwd(),
    encoding: 'utf8',
    stdio: ['ignore', 'pipe', 'pipe'],
  })
}

function tryGit(...args) {
  try {
    return git(...args)
  } catch {
    return null
  }
}

function getLastReleaseTag() {
  const output = tryGit('describe', '--tags', '--match', 'v*', '--abbrev=0')
  return output?.trim() || null
}

function getCommitRecords(lastTag) {
  const range = lastTag ? `${lastTag}..HEAD` : 'HEAD'
  const output = git(
    'log',
    range,
    '--no-merges',
    '--pretty=format:%H%x1f%s%x1f%B%x1e',
  )

  return output
    .split('\x1e')
    .map((record) => record.trim())
    .filter(Boolean)
    .map((record) => {
      const [sha, subject, body] = record.split('\x1f')
      return {
        sha,
        subject: subject?.trim() ?? '',
        body: body?.trim() ?? '',
      }
    })
}

function classifyCommit({ subject, body }) {
  const conventional = /^(?<type>[a-z]+)(?:\((?<scope>[^)]+)\))?(?<breaking>!)?: (?<description>.+)$/.exec(subject)
  const bodyHasBreaking = /(^|\n)BREAKING[ -]CHANGE:/m.test(body)

  if (conventional?.groups) {
    const type = conventional.groups.type.toLowerCase()
    const section = conventionalTypeMap.get(type) ?? 'other'
    return {
      type,
      section,
      description: conventional.groups.description.trim(),
      scope: conventional.groups.scope ?? null,
      breaking: bodyHasBreaking || conventional.groups.breaking === '!',
      releasable: type === 'feat' || type === 'fix' || type === 'perf' || bodyHasBreaking || conventional.groups.breaking === '!',
    }
  }

  const lower = subject.toLowerCase()
  for (const [prefix, section] of fallbackPrefixes) {
    if (!lower.startsWith(prefix)) continue

    const type = section === 'features'
      ? 'feat'
      : section === 'fixes'
        ? 'fix'
        : section === 'performance'
          ? 'perf'
          : 'chore'

    return {
      type,
      section,
      description: subject.trim(),
      scope: null,
      breaking: bodyHasBreaking,
      releasable: section === 'features' || section === 'fixes' || section === 'performance' || bodyHasBreaking,
    }
  }

  return {
    type: 'other',
    section: 'other',
    description: subject.trim(),
    scope: null,
    breaking: bodyHasBreaking,
    releasable: bodyHasBreaking,
  }
}

function determineReleaseLevel(commits) {
  let level = null

  for (const commit of commits) {
    if (commit.breaking) return 'major'
    if (commit.type === 'feat') level = level ?? 'minor'
    if (commit.type === 'fix' || commit.type === 'perf') level = level === 'minor' ? level : 'patch'
  }

  return level
}

function bumpVersion(baseVersion, level, lastTag) {
  if (!lastTag) return '0.1.0'
  return semver.inc(baseVersion, level)
}

function formatCommit(commit) {
  const scope = commit.scope ? `**${commit.scope}:** ` : ''
  return `- ${scope}${commit.description} (${commit.shortSha})`
}

function buildReleaseNotes({ version, lastTag, commits, releasableCommits }) {
  const grouped = {
    features: [],
    fixes: [],
    performance: [],
    docs: [],
    maintenance: [],
    other: [],
    breaking: [],
  }

  for (const commit of commits) {
    grouped[commit.section]?.push(commit)
    if (commit.breaking) grouped.breaking.push(commit)
  }

  const lines = [
    `# AssTrack ${version}`,
    '',
    lastTag
      ? `Changes since ${lastTag}.`
      : 'Initial public release bootstrapped from the current master history.',
    '',
  ]

  if (grouped.breaking.length > 0) {
    lines.push('## Breaking changes', '')
    grouped.breaking.forEach((commit) => lines.push(formatCommit(commit)))
    lines.push('')
  }

  const orderedSections = [
    ['features', 'Features'],
    ['fixes', 'Fixes'],
    ['performance', 'Performance'],
    ['docs', 'Documentation'],
    ['maintenance', 'Maintenance'],
    ['other', 'Other changes'],
  ]

  for (const [key, title] of orderedSections) {
    const sectionCommits = grouped[key]
    if (sectionCommits.length === 0) continue
    lines.push(`## ${title}`, '')
    sectionCommits.forEach((commit) => lines.push(formatCommit(commit)))
    lines.push('')
  }

  if (releasableCommits.length === 0) {
    lines.push('No releasable conventional commits were found.')
    lines.push('')
  }

  return `${lines.join('\n').trim()}\n`
}

function writeGitHubOutput(values) {
  const outputPath = process.env.GITHUB_OUTPUT
  if (!outputPath) return

  const lines = []
  for (const [key, value] of Object.entries(values)) {
    lines.push(`${key}=${value}`)
  }
  writeFileSync(outputPath, `${lines.join('\n')}\n`, { flag: 'a' })
}

const lastTag = getLastReleaseTag()
const baseVersion = lastTag ? lastTag.replace(/^v/, '') : '0.0.0'
const commitRecords = getCommitRecords(lastTag)
const commits = commitRecords.map((record) => {
  const classified = classifyCommit(record)
  return {
    ...record,
    ...classified,
    shortSha: record.sha.slice(0, 7),
  }
})
const releasableCommits = commits.filter((commit) => commit.releasable)
const releaseLevel = determineReleaseLevel(releasableCommits)
const shouldRelease = !lastTag || releaseLevel !== null
const version = shouldRelease ? bumpVersion(baseVersion, releaseLevel ?? 'minor', lastTag) : ''
const tag = version ? `v${version}` : ''
const compareFrom = lastTag || git('rev-list', '--max-parents=0', 'HEAD').trim()
const notes = buildReleaseNotes({ version, lastTag, commits, releasableCommits })

mkdirSync(path.dirname(notesPath), { recursive: true })
writeFileSync(notesPath, notes)

const summary = {
  shouldRelease,
  version,
  tag,
  lastTag: lastTag ?? '',
  compareFrom,
  commitCount: commits.length,
  releasableCommitCount: releasableCommits.length,
  notesPath,
}

writeGitHubOutput({
  should_release: shouldRelease ? 'true' : 'false',
  version,
  tag,
  last_tag: lastTag ?? '',
  compare_from: compareFrom,
  notes_path: notesPath,
  commit_count: String(commits.length),
  releasable_commit_count: String(releasableCommits.length),
})

console.log(JSON.stringify(summary, null, 2))
