import { Dispatch, FormEvent, SetStateAction, useEffect, useMemo, useRef, useState } from 'react'
import {
  createIntegrationFeed,
  deleteIntegrationFeed,
  getIntegrationFeeds,
  getIntegrationProviders,
  updateIntegrationFeed,
  type IntegrationFeed,
  type IntegrationProvider,
} from '../api/integrations'
import { getBridgeLogs, getBridgeMessages, getBridgeStatus, resyncBridgeFeed, type BridgeFeedRuntimeStatus, type BridgeLogEntry, type BridgeRawMessageEntry, type BridgeRuntimeStatus } from '../api/bridgeGateway'
import { useIdentityContext } from '../context/IdentityContext'
import { useLiveDataRefresh } from '../hooks/useLiveDataRefresh'
import DisplayControls from '../components/DisplayControls'

type FeedForm = {
  name: string
  provider: string
  isEnabled: boolean
  autoCreateDevices: boolean
  defaultTags: string
  bridgeEnabled: boolean
  bridgeKey: string
  sharedSecret: string
  baseUrl: string
  accessToken: string
  entities: string
  mqttHost: string
  mqttPort: string
  mqttTopic: string
  mqttUsername: string
  mqttPassword: string
  mqttTls: boolean
  mqttEnabled: boolean
  webhookSource: string
  notes: string
}

const initialForm: FeedForm = {
  name: '',
  provider: 'generic-webhook',
  isEnabled: true,
  autoCreateDevices: true,
  defaultTags: '',
  bridgeEnabled: true,
  bridgeKey: '',
  sharedSecret: '',
  baseUrl: '',
  accessToken: '',
  entities: '',
  mqttHost: '',
  mqttPort: '1883',
  mqttTopic: '',
  mqttUsername: '',
  mqttPassword: '',
  mqttTls: false,
  mqttEnabled: false,
  webhookSource: '',
  notes: '',
}

function detectMeshtasticRegion(): string {
  const locale = Intl.DateTimeFormat().resolvedOptions().locale || navigator.language || 'en-US'
  const region = locale.match(/[-_](?<region>[A-Za-z]{2})\b/)?.groups?.region?.toUpperCase()
  return region || 'US'
}

function standardMeshtasticPublicTopic(region = detectMeshtasticRegion(), channel = 'LongFast'): string {
  return `msh/${region}/2/json/${channel}/#`
}

function standardMeshtasticRootTopic(region = detectMeshtasticRegion()): string {
  return `msh/${region}`
}

const providerGuides: Record<string, { title: string; summary: string; steps: string[]; fields: string[] }> = {
  'meshtastic': {
    title: 'Meshtastic MQTT bridge',
    summary: `Use this when a Meshtastic gateway node publishes JSON position packets to MQTT. This machine resolves to region ${detectMeshtasticRegion()}, so the default public JSON topic is ${standardMeshtasticPublicTopic()}.`,
    steps: [
      'For the standard public channel, use mqtt.meshtastic.org with username meshdev, password large4cats, and the LongFast JSON topic shown below.',
      'On the node, enable MQTT, JSON enabled, and channel uplink for the primary LongFast channel.',
      'For a private channel over the public Meshtastic MQTT server, keep the region root topic, replace LongFast with your private channel name, and enable JSON uplink on that channel.',
      'Precise private locations should use a private channel PSK and should not rely on default public-channel precision; use a private broker if you need full control.',
      'Save the feed; the bridge gateway reloads GUI configuration automatically.',
    ],
    fields: ['Bridge key', 'Shared secret', 'MQTT host', 'MQTT topic', 'Username/password if required'],
  },
  'home-assistant': {
    title: 'Home Assistant location polling',
    summary: 'Use this for Home Assistant device_tracker entities with latitude and longitude attributes.',
    steps: [
      'Create a long-lived access token in Home Assistant.',
      'Find entity IDs such as device_tracker.phone or device_tracker.truck.',
      'Enter the Home Assistant URL, token, and comma-separated entity IDs.',
      'Save the feed; the gateway polls Home Assistant and creates trackers in AssTrack.',
    ],
    fields: ['Home Assistant URL', 'Long-lived token', 'Device tracker entities'],
  },
  'signal': {
    title: 'Signal message bridge',
    summary: 'Use this for a local Signal connector that can poll queued AssTrack messages and post received Signal messages back to the gateway.',
    steps: [
      'Create the feed and keep the shared secret private to the local connector.',
      'Poll the outbound endpoint for queued AssTrack messages.',
      'POST inbound Signal messages with externalPeerId, body, sender, and providerMessageId.',
      'POST delivery status updates after the connector sends, delivers, or fails a queued message.',
    ],
    fields: ['Bridge key', 'Shared secret', 'Outbound messages', 'Inbound messages', 'Delivery status'],
  },
  'telegram': {
    title: 'Telegram bot bridge',
    summary: 'Use this for a Telegram bot worker that forwards chats into AssTrack message threads and sends queued replies back through Telegram.',
    steps: [
      'Create the feed and store the bot token only in the worker or secret manager.',
      'Poll the outbound endpoint for queued AssTrack messages.',
      'POST Telegram updates with chat ID as externalPeerId and the message text as body.',
      'POST delivery status updates when the bot send result is known.',
    ],
    fields: ['Bridge key', 'Shared secret', 'Outbound messages', 'Inbound messages', 'Delivery status'],
  },
  'google-findhub': {
    title: 'Google Find Hub authorized handoff',
    summary: 'Google does not expose a general consumer polling API for Find Hub tag locations.',
    steps: [
      'Use a partner-approved connector, certified accessory flow, or consented export/automation.',
      'Have that connector POST normalized observations to the bridge URL.',
      'Use a stable tag serial, inventory ID, or partner device ID as the external tracker ID.',
    ],
    fields: ['Bridge key', 'Shared secret', 'Webhook / partner source'],
  },
  'samsung-find': {
    title: 'Samsung SmartThings Find authorized handoff',
    summary: 'SmartThings Find tag location requires a Samsung-supported partner/export path.',
    steps: [
      'Confirm the account/device type exposes an approved location export or connector.',
      'Configure that connector to POST normalized observations to the bridge URL.',
      'Use the SmartThings device ID, serial, or internal inventory ID as the external tracker ID.',
    ],
    fields: ['Bridge key', 'Shared secret', 'Webhook / partner source'],
  },
  'generic-webhook': {
    title: 'Generic webhook',
    summary: 'Use this for scripts, automations, partner jobs, or custom services that can POST JSON.',
    steps: [
      'Create the feed and set a bridge key plus shared secret.',
      'Copy the bridge URL after saving.',
      'POST JSON with externalTrackerId, observedAt, latitude, and longitude.',
    ],
    fields: ['Bridge key', 'Shared secret', 'Webhook / partner source'],
  },
  'gps-http': {
    title: 'GPS / cellular HTTP',
    summary: 'Use this for GPS devices or vendor gateways that can send HTTP JSON.',
    steps: [
      'Create the feed and set default tags such as gps, cellular.',
      'Configure the device or vendor gateway to POST location JSON to the bridge URL.',
      'Use the IMEI, serial, or vendor tracker ID as the external tracker ID.',
    ],
    fields: ['Bridge key', 'Shared secret', 'Webhook / partner source'],
  },
}

function ingestUrl(feedId: string) {
  return `${window.location.origin}/api/integrations/${feedId}/observations`
}

function bridgeUrl(formOrFeed: Pick<FeedForm, 'bridgeKey'> | IntegrationFeed) {
  const key = 'bridgeKey' in formOrFeed
    ? formOrFeed.bridgeKey
    : parseConfiguration(formOrFeed.configurationJson).bridgeKey
  const bridgeKey = key?.trim()
  return bridgeKey ? `http://127.0.0.1:5056/bridge/${bridgeKey}` : 'Set a bridge key to generate the endpoint'
}

function bridgeMessagesUrl(formOrFeed: Pick<FeedForm, 'bridgeKey'> | IntegrationFeed, suffix: string) {
  const base = bridgeUrl(formOrFeed)
  return base.startsWith('http') ? `${base}/messages/${suffix}` : base
}

function parseConfiguration(json?: string | null): Partial<FeedForm> {
  if (!json) return {}
  try {
    const value = JSON.parse(json) as Record<string, unknown>
    return {
      bridgeEnabled: value.bridgeEnabled !== false,
      bridgeKey: stringValue(value.bridgeKey),
      sharedSecret: stringValue(value.sharedSecret),
      baseUrl: stringValue(value.baseUrl),
      accessToken: stringValue(value.accessToken),
      entities: Array.isArray(value.entities) ? value.entities.join(', ') : stringValue(value.entities),
      mqttHost: stringValue(value.mqttHost),
      mqttPort: stringValue(value.mqttPort) || '1883',
      mqttTopic: stringValue(value.mqttTopic) || standardMeshtasticPublicTopic(),
      mqttUsername: stringValue(value.mqttUsername),
      mqttPassword: stringValue(value.mqttPassword),
      mqttTls: value.mqttTls === true,
      mqttEnabled: value.mqttEnabled === true,
      webhookSource: stringValue(value.webhookSource),
      notes: stringValue(value.notes),
    }
  } catch {
    return {}
  }
}

function stringValue(value: unknown): string {
  return typeof value === 'string' ? value : typeof value === 'number' || typeof value === 'boolean' ? String(value) : ''
}

function serializeConfiguration(form: FeedForm): string | null {
  const config: Record<string, unknown> = {
    bridgeEnabled: form.bridgeEnabled,
    bridgeKey: form.bridgeKey.trim(),
    sharedSecret: form.sharedSecret.trim(),
  }

  if (form.provider === 'meshtastic') {
    config.mqttEnabled = form.mqttEnabled
    config.mqttHost = form.mqttHost.trim()
    config.mqttPort = form.mqttPort.trim()
    config.mqttTopic = form.mqttTopic.trim()
    config.mqttUsername = form.mqttUsername.trim()
    config.mqttPassword = form.mqttPassword.trim()
    config.mqttTls = form.mqttTls
  }

  if (form.provider === 'home-assistant') {
    config.pollingEnabled = true
    config.baseUrl = form.baseUrl.trim()
    config.accessToken = form.accessToken.trim()
    config.entities = form.entities.split(',').map((item) => item.trim()).filter(Boolean)
  }

  if (['google-findhub', 'samsung-find', 'apple-findmy', 'generic-webhook', 'gps-http', 'signal', 'telegram'].includes(form.provider)) {
    config.webhookSource = form.webhookSource.trim()
  }

  if (form.notes.trim()) config.notes = form.notes.trim()

  Object.keys(config).forEach((key) => {
    const value = config[key]
    if (value === '' || (Array.isArray(value) && value.length === 0)) delete config[key]
  })

  return Object.keys(config).length === 0 ? null : JSON.stringify(config, null, 2)
}

function applyProviderDefaults(form: FeedForm, provider: string): FeedForm {
  const oldProvider = form.provider
  const shouldReplaceBridgeKey = !form.bridgeKey || form.bridgeKey === oldProvider
  const next = { ...form, provider, bridgeKey: shouldReplaceBridgeKey ? provider : form.bridgeKey }
  if (provider === 'meshtastic') {
    next.defaultTags ||= 'meshtastic, lora'
    next.mqttHost ||= 'mqtt.meshtastic.org'
    next.mqttUsername ||= 'meshdev'
    next.mqttPassword ||= 'large4cats'
    next.mqttTopic ||= standardMeshtasticPublicTopic()
    next.mqttPort ||= '1883'
    next.mqttEnabled = true
  }
  if (provider === 'home-assistant') {
    next.defaultTags ||= 'home-assistant, device-tracker'
  }
  if (provider === 'google-findhub') next.defaultTags ||= 'google, find-hub'
  if (provider === 'samsung-find') next.defaultTags ||= 'samsung, smartthings'
  if (provider === 'signal') {
    next.defaultTags ||= 'signal, messaging'
    next.autoCreateDevices = false
    next.webhookSource ||= 'local-signal-bridge'
  }
  if (provider === 'telegram') {
    next.defaultTags ||= 'telegram, messaging'
    next.autoCreateDevices = false
    next.webhookSource ||= 'telegram-bot-bridge'
  }
  return next
}

function getGuide(provider: string) {
  return providerGuides[provider] ?? providerGuides['generic-webhook']
}

function formatRuntimeDate(value?: string | null) {
  return value ? new Date(value).toLocaleTimeString() : 'Never'
}

function runtimeForFeed(feed: IntegrationFeed, status: BridgeRuntimeStatus | null): BridgeFeedRuntimeStatus | undefined {
  return status?.feeds.find((item) => item.feedId === feed.id)
}

function statusClass(state?: string) {
  if (!state) return 'status-warn'
  if (['subscribed', 'receiving', 'delivering', 'http-request', 'message-request'].includes(state)) return 'status-ok'
  if (['connecting', 'resync-requested'].includes(state)) return 'status-polling'
  if (['disabled', 'disconnected'].includes(state)) return 'status-warn'
  return 'status-bad'
}

function matchesText(value: string | null | undefined, search: string): boolean {
  return value?.toLowerCase().includes(search.toLowerCase()) === true
}

function filterBridgeMessages(
  messages: BridgeRawMessageEntry[],
  filters: {
    search: string
    trackerId: string
    topic: string
    messageType: string
    payloadOnly: boolean
  },
): BridgeRawMessageEntry[] {
  const search = filters.search.trim()
  const trackerId = filters.trackerId.trim()
  const topic = filters.topic.trim()
  const messageType = filters.messageType.trim()

  return messages.filter((entry) => {
    if (trackerId && !matchesText(entry.trackerId, trackerId) && !matchesText(entry.payload, trackerId)) return false
    if (topic && !matchesText(entry.topic, topic)) return false
    if (messageType && !matchesText(entry.messageType, messageType) && !matchesText(entry.payload, messageType)) return false
    if (search) {
      if (filters.payloadOnly) return matchesText(entry.payload, search)
      return (
        matchesText(entry.feedKey, search) ||
        matchesText(entry.provider, search) ||
        matchesText(entry.topic, search) ||
        matchesText(entry.trackerId, search) ||
        matchesText(entry.messageType, search) ||
        matchesText(entry.summary, search) ||
        matchesText(entry.payload, search)
      )
    }
    return true
  })
}

function BridgeConfigEditor({ form, onChange }: { form: FeedForm; onChange: Dispatch<SetStateAction<FeedForm>> }) {
  const guide = getGuide(form.provider)

  return (
    <div className="field field-wide bridge-editor">
      <div className="bridge-editor-header">
        <span>{guide.title}</span>
        <label className="check-field">
          <input checked={form.bridgeEnabled} onChange={(event) => onChange((value) => ({ ...value, bridgeEnabled: event.target.checked }))} type="checkbox" />
          <span>Bridge enabled</span>
        </label>
      </div>

      <div className="guide-panel">
        <p className="muted">{guide.summary}</p>
        <ol>
          {guide.steps.map((step) => <li key={step}>{step}</li>)}
        </ol>
        <div className="tag-list">
          {guide.fields.map((field) => <span className="badge" key={field}>{field}</span>)}
        </div>
      </div>

      <div className="field-grid">
        <label className="field">
          <span>Bridge key</span>
          <input name="bridgeKey" onChange={(event) => onChange((value) => ({ ...value, bridgeKey: event.target.value }))} placeholder="fleet-meshtastic" value={form.bridgeKey} />
        </label>
        <label className="field">
          <span>Shared secret</span>
          <input onChange={(event) => onChange((value) => ({ ...value, sharedSecret: event.target.value }))} placeholder="required for webhook posts" type="password" value={form.sharedSecret} />
        </label>
      </div>

      {form.provider === 'meshtastic' && (
        <div className="field-grid">
          <label className="check-field">
            <input checked={form.mqttEnabled} onChange={(event) => onChange((value) => ({ ...value, mqttEnabled: event.target.checked }))} type="checkbox" />
            <span>Subscribe to MQTT</span>
          </label>
          <label className="field">
            <span>MQTT host</span>
            <input onChange={(event) => onChange((value) => ({ ...value, mqttHost: event.target.value }))} placeholder="mqtt.example.local" value={form.mqttHost} />
          </label>
          <label className="field">
            <span>MQTT port</span>
            <input onChange={(event) => onChange((value) => ({ ...value, mqttPort: event.target.value }))} placeholder="1883" value={form.mqttPort} />
          </label>
          <label className="field">
            <span>Topic</span>
            <input onChange={(event) => onChange((value) => ({ ...value, mqttTopic: event.target.value }))} value={form.mqttTopic} />
          </label>
          <div className="notice notice-info field-wide">
            <strong>Public Meshtastic MQTT defaults</strong>
            <span className="muted">
              Region root: {standardMeshtasticRootTopic()}. Standard public JSON topic: {standardMeshtasticPublicTopic()}.
              For a private channel on the public server, keep the region root and replace LongFast with your exact channel name.
            </span>
          </div>
          <label className="field">
            <span>Username</span>
            <input onChange={(event) => onChange((value) => ({ ...value, mqttUsername: event.target.value }))} value={form.mqttUsername} />
          </label>
          <label className="field">
            <span>Password</span>
            <input onChange={(event) => onChange((value) => ({ ...value, mqttPassword: event.target.value }))} type="password" value={form.mqttPassword} />
          </label>
          <label className="check-field">
            <input checked={form.mqttTls} onChange={(event) => onChange((value) => ({ ...value, mqttTls: event.target.checked }))} type="checkbox" />
            <span>TLS</span>
          </label>
        </div>
      )}

      {form.provider === 'home-assistant' && (
        <div className="field-grid">
          <label className="field">
            <span>Home Assistant URL</span>
            <input onChange={(event) => onChange((value) => ({ ...value, baseUrl: event.target.value }))} placeholder="http://homeassistant.local:8123" value={form.baseUrl} />
          </label>
          <label className="field">
            <span>Long-lived token</span>
            <input onChange={(event) => onChange((value) => ({ ...value, accessToken: event.target.value }))} type="password" value={form.accessToken} />
          </label>
          <label className="field field-wide">
            <span>Device tracker entities</span>
            <input onChange={(event) => onChange((value) => ({ ...value, entities: event.target.value }))} placeholder="device_tracker.phone, device_tracker.truck" value={form.entities} />
          </label>
        </div>
      )}

      {['google-findhub', 'samsung-find', 'apple-findmy', 'generic-webhook', 'gps-http', 'signal', 'telegram'].includes(form.provider) && (
        <div className="field-grid">
          <label className="field field-wide">
            <span>Webhook / partner source</span>
            <input
              onChange={(event) => onChange((value) => ({ ...value, webhookSource: event.target.value }))}
              placeholder="Authorized automation, partner connector, or export job name"
              value={form.webhookSource}
            />
          </label>
        </div>
      )}

      {['signal', 'telegram'].includes(form.provider) && (
        <div className="notice notice-info field-wide">
          <strong>Message bridge endpoints</strong>
          <span className="muted">Outbound: {bridgeMessagesUrl(form, 'outbound')}</span>
          <span className="muted">Inbound: {bridgeMessagesUrl(form, 'inbound')}</span>
          <span className="muted">Status: {bridgeMessagesUrl(form, '{messageId}/status')}</span>
        </div>
      )}

      <label className="field">
        <span>Notes</span>
        <textarea onChange={(event) => onChange((value) => ({ ...value, notes: event.target.value }))} rows={2} value={form.notes} />
      </label>
      <div className="muted">Bridge endpoint: {bridgeUrl(form)}</div>
    </div>
  )
}

export default function IntegrationsPage() {
  const { isOperator, loading: identityLoading } = useIdentityContext()
  const [integrationViewMode, setIntegrationViewMode] = useState<'cards' | 'table'>('cards')
  const [providers, setProviders] = useState<IntegrationProvider[]>([])
  const [feeds, setFeeds] = useState<IntegrationFeed[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [showCreate, setShowCreate] = useState(false)
  const [form, setForm] = useState<FeedForm>(initialForm)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editForm, setEditForm] = useState<FeedForm>(initialForm)
  const createFormRef = useRef<HTMLFormElement | null>(null)
  const [bridgeStatus, setBridgeStatus] = useState<BridgeRuntimeStatus | null>(null)
  const [bridgeLogs, setBridgeLogs] = useState<BridgeLogEntry[]>([])
  const [bridgeMessages, setBridgeMessages] = useState<BridgeRawMessageEntry[]>([])
  const [bridgeError, setBridgeError] = useState<string | null>(null)
  const [selectedLogFeed, setSelectedLogFeed] = useState<string>('')
  const [messageSearch, setMessageSearch] = useState('')
  const [messageTrackerFilter, setMessageTrackerFilter] = useState('')
  const [messageTopicFilter, setMessageTopicFilter] = useState('')
  const [messageTypeFilter, setMessageTypeFilter] = useState('')
  const [messagePayloadOnly, setMessagePayloadOnly] = useState(false)
  const [expandedMessageKey, setExpandedMessageKey] = useState<string | null>(null)

  async function load() {
    try {
      setError(null)
      const [providerItems, feedItems] = await Promise.all([getIntegrationProviders(), getIntegrationFeeds()])
      setProviders(providerItems)
      setFeeds(feedItems)
      if (providerItems.length > 0 && form.provider === '') {
        setForm((value) => ({ ...value, provider: providerItems[0].id }))
      }
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e))
    } finally {
      setLoading(false)
    }
  }

  async function loadBridgeRuntime() {
    try {
      setBridgeError(null)
      const [status, logs, messages] = await Promise.all([
        getBridgeStatus(),
        getBridgeLogs(selectedLogFeed || undefined),
        getBridgeMessages({
          feedKey: selectedLogFeed || undefined,
          search: messageSearch,
          trackerId: messageTrackerFilter,
          topic: messageTopicFilter,
          messageType: messageTypeFilter,
          payloadOnly: messagePayloadOnly,
          limit: 300,
        }),
      ])
      setBridgeStatus(status)
      setBridgeLogs(logs)
      setBridgeMessages(filterBridgeMessages(messages, {
        search: messageSearch,
        trackerId: messageTrackerFilter,
        topic: messageTopicFilter,
        messageType: messageTypeFilter,
        payloadOnly: messagePayloadOnly,
      }))
    } catch (e: unknown) {
      setBridgeError(e instanceof Error ? e.message : String(e))
    }
  }

  useEffect(() => {
    if (identityLoading || !isOperator) return
    void load()
  }, [identityLoading, isOperator])

  useLiveDataRefresh(async () => {
    await Promise.all([load(), loadBridgeRuntime()])
  }, { eventTypes: ['data_changed'], debounceMs: 1200, enabled: !identityLoading && isOperator })

  useEffect(() => {
    if (identityLoading || !isOperator) return
    void loadBridgeRuntime()
    const timer = window.setInterval(() => {
      void loadBridgeRuntime()
    }, 5000)
    return () => window.clearInterval(timer)
  }, [identityLoading, isOperator, messagePayloadOnly, messageSearch, messageTopicFilter, messageTrackerFilter, messageTypeFilter, selectedLogFeed])

  const providerById = useMemo(
    () => new Map(providers.map((provider) => [provider.id, provider])),
    [providers],
  )

  async function handleCreate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmitting(true)
    try {
      await createIntegrationFeed({
        name: form.name.trim(),
        provider: form.provider,
        isEnabled: form.isEnabled,
        autoCreateDevices: form.autoCreateDevices,
        defaultTags: form.defaultTags.trim() || null,
        configurationJson: serializeConfiguration(form),
      })
      setForm(initialForm)
      setShowCreate(false)
      await load()
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e))
    } finally {
      setSubmitting(false)
    }
  }

  function startEdit(feed: IntegrationFeed) {
    setEditingId(feed.id)
    setEditForm({
      ...initialForm,
      name: feed.name,
      provider: feed.provider,
      isEnabled: feed.isEnabled,
      autoCreateDevices: feed.autoCreateDevices,
      defaultTags: feed.defaultTags ?? '',
      ...parseConfiguration(feed.configurationJson),
    })
  }

  async function saveEdit() {
    if (!editingId) return
    setSubmitting(true)
    try {
      await updateIntegrationFeed(editingId, {
        name: editForm.name.trim(),
        isEnabled: editForm.isEnabled,
        autoCreateDevices: editForm.autoCreateDevices,
        defaultTags: editForm.defaultTags.trim() || null,
        configurationJson: serializeConfiguration(editForm),
      })
      setEditingId(null)
      await load()
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e))
    } finally {
      setSubmitting(false)
    }
  }

  async function handleDelete(feed: IntegrationFeed) {
    if (!window.confirm(`Delete integration feed "${feed.name}"? Devices will remain but lose their feed link.`)) return
    setSubmitting(true)
    try {
      await deleteIntegrationFeed(feed.id)
      await load()
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e))
    } finally {
      setSubmitting(false)
    }
  }

  async function handleResync(feedKey: string) {
    try {
      setBridgeError(null)
      await resyncBridgeFeed(feedKey)
      await loadBridgeRuntime()
    } catch (e: unknown) {
      setBridgeError(e instanceof Error ? e.message : String(e))
    }
  }

  if (!isOperator && !identityLoading) {
    return (
      <div className="card">
        <h1>Integrations</h1>
        <p className="muted">Integration management requires an operator key.</p>
      </div>
    )
  }

  if (loading || identityLoading) return <div className="card">Loading integrations...</div>
  if (error) return <div className="card">Error: {error}</div>

  const selectedProvider = providerById.get(form.provider)

  function openCreate(provider?: string) {
    const nextProvider = provider ?? initialForm.provider
    const providerName = providerById.get(nextProvider)?.name ?? nextProvider
    const nextForm = applyProviderDefaults({
      ...initialForm,
      provider: nextProvider,
      bridgeKey: nextProvider,
      name: `${providerName} bridge`,
    }, nextProvider)

    setForm(nextForm)
    setShowCreate(true)
    window.setTimeout(() => {
      createFormRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
      createFormRef.current?.querySelector<HTMLInputElement>('input[name="bridgeKey"]')?.focus()
    }, 0)
  }

  return (
    <div className="section">
      <div className="page-header">
        <div>
          <h1>Bridge Gateway</h1>
          <p className="muted">Configure location providers, bridge endpoints, API keys, polling, MQTT subscriptions, and provider-specific handoffs.</p>
        </div>
        <div className="ops-actions">
          <DisplayControls mode={integrationViewMode} onModeChange={setIntegrationViewMode} />
          <button className="button button-secondary" onClick={() => showCreate ? setShowCreate(false) : openCreate()} type="button">
            {showCreate ? 'Cancel' : 'Add Bridge Feed'}
          </button>
        </div>
      </div>

      <div className="guide-grid">
        <article className="guide-card">
          <span className="badge badge-success">Step 1</span>
          <h3>Choose a provider</h3>
          <p className="muted">Pick Meshtastic, Home Assistant, Google Find Hub, Samsung SmartThings, GPS HTTP, or a generic webhook.</p>
        </article>
        <article className="guide-card">
          <span className="badge badge-success">Step 2</span>
          <h3>Configure the bridge</h3>
          <p className="muted">Set a bridge key, secret, and provider settings. The gateway reads this configuration from AssTrack automatically.</p>
        </article>
        <article className="guide-card">
          <span className="badge badge-success">Step 3</span>
          <h3>Connect the source</h3>
          <p className="muted">Point webhooks to the bridge URL, enable Meshtastic MQTT, or let the gateway poll Home Assistant entities.</p>
        </article>
      </div>

      <div className="metrics">
        <div className="metric"><span>Providers</span><strong>{providers.length}</strong></div>
        <div className="metric"><span>Feeds</span><strong>{feeds.length}</strong></div>
        <div className="metric"><span>Enabled</span><strong>{feeds.filter((feed) => feed.isEnabled).length}</strong></div>
        <div className="metric"><span>Trackers</span><strong>{feeds.reduce((total, feed) => total + feed.deviceCount, 0)}</strong></div>
      </div>

      {bridgeStatus?.dryRun && (
        <div className="notice notice-warning">
          <strong>Bridge is in dry-run mode</strong>
          <span className="muted">Incoming bridge data will be parsed but not written to AssTrack. Set `ASSTRACK_BRIDGE_DRY_RUN=false` or remove the dry-run override before expecting live trackers.</span>
        </div>
      )}

      {bridgeError && (
        <div className="notice notice-danger">
          <strong>Bridge gateway unavailable</strong>
          <span className="muted">{bridgeError}</span>
        </div>
      )}

      <div className="card">
        <div className="page-header">
          <div>
            <h2>Live Bridge Monitor</h2>
            <p className="muted">Connection state, MQTT messages, parsed observations, deliveries, and recent gateway log entries.</p>
          </div>
          <div className="compact-actions">
            <label className="field compact-field">
              <span>Feed</span>
              <select onChange={(event) => setSelectedLogFeed(event.target.value)} value={selectedLogFeed}>
                <option value="">All feeds</option>
                {bridgeStatus?.feeds.map((item) => (
                  <option key={item.feedKey} value={item.feedKey}>{item.feedKey}</option>
                ))}
              </select>
            </label>
            <label className="field compact-field">
              <span>Search MQTT</span>
              <input
                onChange={(event) => setMessageSearch(event.target.value)}
                placeholder="any string"
                value={messageSearch}
              />
            </label>
            <button className="button button-secondary" onClick={() => void loadBridgeRuntime()} type="button">Refresh</button>
          </div>
        </div>

        <div className="panel-grid">
          {(bridgeStatus?.feeds ?? []).map((item) => (
            <div className="status-tile" key={item.feedKey}>
              <span className={`status-line ${statusClass(item.state)}`}>{item.state}</span>
              <strong>{item.feedKey}</strong>
              <span className="muted">{item.provider}</span>
              <span className="muted">Topic: {item.topic ?? 'Not subscribed'}</span>
              <span className="muted">Messages: {item.messagesReceived} • Parsed: {item.observationsParsed} • Delivered: {item.observationsDelivered}</span>
              <span className="muted">Last message: {formatRuntimeDate(item.lastMessageAt)}</span>
              {item.lastTrackerId && <span className="muted">Last tracker: {item.lastTrackerId}</span>}
              {item.lastError && <span className="error-text">{item.lastError}</span>}
              <button className="button button-secondary" onClick={() => void handleResync(item.feedKey)} type="button">Resync</button>
            </div>
          ))}
          {bridgeStatus && bridgeStatus.feeds.length === 0 && (
            <div className="status-tile">
              <span className="muted">No runtime feeds</span>
              <strong>Waiting for bridge config</strong>
            </div>
          )}
        </div>

        <div className="bridge-message-viewer">
          <div className="bridge-message-header">
            <h3>MQTT Messages</h3>
            <span className="muted">{bridgeMessages.length} retained messages</span>
          </div>
          <div className="bridge-message-filters">
            <label className="field compact-field">
              <span>Device / tracker</span>
              <input
                onChange={(event) => setMessageTrackerFilter(event.target.value)}
                placeholder="!12f4fb74 or numeric id"
                value={messageTrackerFilter}
              />
            </label>
            <label className="field compact-field">
              <span>Topic contains</span>
              <input
                onChange={(event) => setMessageTopicFilter(event.target.value)}
                placeholder="JDH, LongFast, node id"
                value={messageTopicFilter}
              />
            </label>
            <label className="field compact-field">
              <span>Type</span>
              <input
                onChange={(event) => setMessageTypeFilter(event.target.value)}
                placeholder="position, nodeinfo, telemetry"
                value={messageTypeFilter}
              />
            </label>
            <label className="check-field bridge-message-check">
              <input checked={messagePayloadOnly} onChange={(event) => setMessagePayloadOnly(event.target.checked)} type="checkbox" />
              <span>Search raw payload only</span>
            </label>
            <button
              className="button button-secondary"
              onClick={() => {
                setMessageSearch('')
                setMessageTrackerFilter('')
                setMessageTopicFilter('')
                setMessageTypeFilter('')
                setMessagePayloadOnly(false)
              }}
              type="button"
            >
              Clear Filters
            </button>
          </div>
          <div className="bridge-message-list">
            {bridgeMessages.map((entry) => {
              const key = `${entry.timestamp}-${entry.feedKey}-${entry.topic ?? ''}-${entry.summary || entry.payload}`
              const timestamp = Number.isNaN(new Date(entry.timestamp).getTime())
                ? entry.timestamp
                : new Date(entry.timestamp).toLocaleTimeString()
              return (
                <details
                  className="bridge-message-row"
                  key={key}
                  onToggle={(event) => setExpandedMessageKey(event.currentTarget.open ? key : null)}
                  open={expandedMessageKey === key}
                >
                  <summary className="bridge-message-summary">
                    <span className="bridge-message-time">{timestamp}</span>
                    <strong>{entry.trackerId || entry.feedKey || 'Unknown tracker'}</strong>
                    <span className="badge">{entry.messageType || entry.provider || 'message'}</span>
                    <span className="coords">{entry.topic || 'No topic'}</span>
                    <span className="bridge-message-summary-text">{entry.summary || entry.payload || 'No payload captured'}</span>
                  </summary>
                  <div className="bridge-message-detail">
                    <div className="bridge-raw-payload-frame">
                      <pre className="bridge-raw-payload">{entry.payload || entry.summary || 'No payload captured.'}</pre>
                    </div>
                  </div>
                </details>
              )
            })}
            {bridgeMessages.length === 0 && <p className="muted">No retained MQTT messages match the current filters.</p>}
          </div>
        </div>

        <div className="bridge-log-list">
          {bridgeLogs.map((entry) => (
            <div className="bridge-log-row" key={`${entry.timestamp}-${entry.feedKey}-${entry.message}`}>
              <span>{new Date(entry.timestamp).toLocaleTimeString()}</span>
              <span className={`badge ${entry.level === 'warn' ? 'badge-warning' : entry.level === 'error' ? 'badge-danger' : 'badge-success'}`}>{entry.level}</span>
              <strong>{entry.feedKey}</strong>
              <span>{entry.message}</span>
            </div>
          ))}
          {bridgeLogs.length === 0 && <p className="muted">No bridge logs yet. Click Resync or wait for MQTT/webhook activity.</p>}
        </div>
      </div>

      {integrationViewMode === 'cards' ? (
      <div className="asset-grid">
        {providers.map((provider) => (
          <article className="list-card" key={provider.id}>
            <header>
              <h3>{provider.name}</h3>
              <span className={`badge ${provider.status === 'ready' ? 'badge-success' : 'badge-warning'}`}>{provider.status}</span>
            </header>
            <p className="muted">{provider.description}</p>
            <div className="asset-meta">
              <div className="asset-meta-row"><span>Category</span><strong>{provider.category}</strong></div>
              <div className="asset-meta-row"><span>Mode</span><strong>{provider.connectionMode}</strong></div>
              <div className="asset-meta-row"><span>Direct API</span><strong>{provider.supportsDirectApi ? 'Yes' : 'No'}</strong></div>
            </div>
            <p className="muted">{provider.setupNotes}</p>
            <div className="button-row">
              <button className="button button-secondary" onClick={() => openCreate(provider.id)} type="button">Configure</button>
            </div>
          </article>
        ))}
      </div>
      ) : (
      <div className="card table-card">
        <h2>Providers</h2>
        <table className="data-table">
          <thead>
            <tr>
              <th>Provider</th>
              <th>Category</th>
              <th>Mode</th>
              <th>Status</th>
              <th>Direct API</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {providers.map((provider) => (
              <tr key={provider.id}>
                <td>
                  <strong>{provider.name}</strong>
                  <div className="muted">{provider.description}</div>
                </td>
                <td>{provider.category}</td>
                <td>{provider.connectionMode}</td>
                <td><span className={`badge ${provider.status === 'ready' ? 'badge-success' : 'badge-warning'}`}>{provider.status}</span></td>
                <td>{provider.supportsDirectApi ? 'Yes' : 'No'}</td>
                <td><button className="button button-secondary button-compact" onClick={() => openCreate(provider.id)} type="button">Configure</button></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      )}

      {showCreate && (
        <form className="card inline-form" data-testid="bridge-feed-form" onSubmit={handleCreate} ref={createFormRef}>
          <div className="field-grid">
            <label className="field">
              <span>Name</span>
              <input onChange={(event) => setForm((value) => ({ ...value, name: event.target.value }))} required value={form.name} />
            </label>
            <label className="field">
              <span>Provider</span>
              <select onChange={(event) => setForm((value) => applyProviderDefaults(value, event.target.value))} value={form.provider}>
                {providers.map((provider) => (
                  <option key={provider.id} value={provider.id}>{provider.name}</option>
                ))}
              </select>
            </label>
            <label className="field">
              <span>Default tags</span>
              <input
                onChange={(event) => setForm((value) => ({ ...value, defaultTags: event.target.value }))}
                placeholder={selectedProvider?.recommendedTags.join(', ') ?? 'gps, fleet'}
                value={form.defaultTags}
              />
            </label>
            <BridgeConfigEditor form={form} onChange={setForm} />
          </div>
          <div className="compact-actions">
            <label className="check-field">
              <input checked={form.isEnabled} onChange={(event) => setForm((value) => ({ ...value, isEnabled: event.target.checked }))} type="checkbox" />
              <span>Enabled</span>
            </label>
            <label className="check-field">
              <input checked={form.autoCreateDevices} onChange={(event) => setForm((value) => ({ ...value, autoCreateDevices: event.target.checked }))} type="checkbox" />
              <span>Auto-create trackers</span>
            </label>
          </div>
          <div className="button-row">
            <button className="button" disabled={submitting} type="submit">{submitting ? 'Saving...' : 'Create feed'}</button>
          </div>
        </form>
      )}

      <div className="section">
        <h2>Configured Feeds</h2>
        {integrationViewMode === 'table' && feeds.length > 0 && (
          <div className="card table-card">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Feed</th>
                  <th>Provider</th>
                  <th>Status</th>
                  <th>Runtime</th>
                  <th>Trackers</th>
                  <th>Manage</th>
                </tr>
              </thead>
              <tbody>
                {feeds.map((feed) => (
                  <tr key={feed.id}>
                    <td>
                      <strong>{feed.name}</strong>
                      <div className="muted">{bridgeUrl(feed)}</div>
                    </td>
                    <td>{feed.providerName}</td>
                    <td><span className={`badge ${feed.isEnabled ? 'badge-success' : 'badge-danger'}`}>{feed.isEnabled ? 'Enabled' : 'Disabled'}</span></td>
                    <td>{runtimeForFeed(feed, bridgeStatus)?.state ?? 'Not connected'}</td>
                    <td>{feed.deviceCount}</td>
                    <td>
                      <div className="compact-actions">
                        <button className="button button-secondary button-compact" onClick={() => startEdit(feed)} type="button">Edit</button>
                        <button className="button button-secondary button-compact" onClick={() => void navigator.clipboard.writeText(bridgeUrl(feed))} type="button">Copy</button>
                        <button className="button button-danger button-compact" disabled={submitting} onClick={() => void handleDelete(feed)} type="button">Delete</button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {integrationViewMode === 'table' && editingId && (
          <div className="card inline-form">
            <div className="field-grid">
              <label className="field">
                <span>Name</span>
                <input onChange={(event) => setEditForm((value) => ({ ...value, name: event.target.value }))} required value={editForm.name} />
              </label>
              <label className="field">
                <span>Default tags</span>
                <input onChange={(event) => setEditForm((value) => ({ ...value, defaultTags: event.target.value }))} value={editForm.defaultTags} />
              </label>
              <BridgeConfigEditor form={editForm} onChange={setEditForm} />
            </div>
            <div className="compact-actions">
              <label className="check-field">
                <input checked={editForm.isEnabled} onChange={(event) => setEditForm((value) => ({ ...value, isEnabled: event.target.checked }))} type="checkbox" />
                <span>Enabled</span>
              </label>
              <label className="check-field">
                <input checked={editForm.autoCreateDevices} onChange={(event) => setEditForm((value) => ({ ...value, autoCreateDevices: event.target.checked }))} type="checkbox" />
                <span>Auto-create trackers</span>
              </label>
            </div>
            <div className="button-row">
              <button className="button" disabled={submitting} onClick={() => void saveEdit()} type="button">Save</button>
              <button className="button button-secondary" onClick={() => setEditingId(null)} type="button">Cancel</button>
            </div>
          </div>
        )}
        {integrationViewMode === 'cards' && (
        <>
        {feeds.map((feed) => (
          <article className="list-card" key={feed.id}>
            {editingId === feed.id ? (
              <div className="inline-form">
                <div className="field-grid">
                  <label className="field">
                    <span>Name</span>
                    <input onChange={(event) => setEditForm((value) => ({ ...value, name: event.target.value }))} required value={editForm.name} />
                  </label>
                  <label className="field">
                    <span>Default tags</span>
                    <input onChange={(event) => setEditForm((value) => ({ ...value, defaultTags: event.target.value }))} value={editForm.defaultTags} />
                  </label>
                  <BridgeConfigEditor form={editForm} onChange={setEditForm} />
                </div>
                <div className="compact-actions">
                  <label className="check-field">
                    <input checked={editForm.isEnabled} onChange={(event) => setEditForm((value) => ({ ...value, isEnabled: event.target.checked }))} type="checkbox" />
                    <span>Enabled</span>
                  </label>
                  <label className="check-field">
                    <input checked={editForm.autoCreateDevices} onChange={(event) => setEditForm((value) => ({ ...value, autoCreateDevices: event.target.checked }))} type="checkbox" />
                    <span>Auto-create trackers</span>
                  </label>
                </div>
                <div className="button-row">
                  <button className="button" disabled={submitting} onClick={() => void saveEdit()} type="button">Save</button>
                  <button className="button button-secondary" onClick={() => setEditingId(null)} type="button">Cancel</button>
                </div>
              </div>
            ) : (
              <>
                <header>
                  <h3>{feed.name}</h3>
                  <span className={`badge ${feed.isEnabled ? 'badge-success' : 'badge-danger'}`}>{feed.isEnabled ? 'Enabled' : 'Disabled'}</span>
                  <span className="badge">{feed.providerName}</span>
                </header>
                <div className="asset-meta">
                  {runtimeForFeed(feed, bridgeStatus) && (
                    <>
                      <div className="asset-meta-row"><span>Bridge state</span><strong>{runtimeForFeed(feed, bridgeStatus)?.state}</strong></div>
                      <div className="asset-meta-row"><span>Messages</span><strong>{runtimeForFeed(feed, bridgeStatus)?.messagesReceived ?? 0}</strong></div>
                      <div className="asset-meta-row"><span>Delivered</span><strong>{runtimeForFeed(feed, bridgeStatus)?.observationsDelivered ?? 0}</strong></div>
                    </>
                  )}
                  <div className="asset-meta-row"><span>Trackers</span><strong>{feed.deviceCount}</strong></div>
                  <div className="asset-meta-row"><span>Auto-create</span><strong>{feed.autoCreateDevices ? 'On' : 'Off'}</strong></div>
                  <div className="asset-meta-row"><span>Default tags</span><strong>{feed.defaultTags ?? 'None'}</strong></div>
                  <div className="asset-meta-row"><span>Ingest URL</span><strong className="coords">{ingestUrl(feed.id)}</strong></div>
                  <div className="asset-meta-row"><span>Bridge URL</span><strong className="coords">{bridgeUrl(feed)}</strong></div>
                </div>
                <div className="button-row">
                  <button className="button button-secondary" onClick={() => startEdit(feed)} type="button">Edit</button>
                  <button className="button button-secondary" onClick={() => void navigator.clipboard.writeText(ingestUrl(feed.id))} type="button">Copy URL</button>
                  <button className="button button-secondary" onClick={() => void navigator.clipboard.writeText(bridgeUrl(feed))} type="button">Copy Bridge</button>
                  {runtimeForFeed(feed, bridgeStatus)?.feedKey && (
                    <button className="button button-secondary" onClick={() => void handleResync(runtimeForFeed(feed, bridgeStatus)!.feedKey)} type="button">Resync</button>
                  )}
                  <button className="button button-danger" disabled={submitting} onClick={() => void handleDelete(feed)} type="button">Delete</button>
                </div>
              </>
            )}
          </article>
        ))}
        {feeds.length === 0 && (
          <div className="card empty-state">
            <h3>No bridge feeds configured</h3>
            <p className="muted">Start with Home Assistant for local device trackers, Meshtastic for MQTT mesh locations, or a generic webhook for custom providers.</p>
            <div className="button-row">
              <button className="button" onClick={() => openCreate('home-assistant')} type="button">Add Home Assistant</button>
              <button className="button button-secondary" onClick={() => openCreate('meshtastic')} type="button">Add Meshtastic</button>
              <button className="button button-secondary" onClick={() => openCreate('generic-webhook')} type="button">Add Webhook</button>
            </div>
          </div>
        )}
        </>
        )}
      </div>
    </div>
  )
}
