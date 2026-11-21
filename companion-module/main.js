// Bitfocus Companion module for DMX Light Control (formerly DmxLicht and Glowsmith).
// Talks to the app's HTTP API (/api/cmd/...) and follows its state over the WebSocket (/ws).
const { InstanceBase, InstanceStatus, runEntrypoint, combineRgb } = require('@companion-module/base')

const BLACK = combineRgb(0, 0, 0)
const WHITE = combineRgb(255, 255, 255)
const ACCENT = combineRgb(252, 178, 78)
const RED = combineRgb(200, 30, 30)
const GREEN = combineRgb(30, 160, 70)

const hexToRgb = (hex) => {
	const n = parseInt(String(hex || '#333333').replace('#', ''), 16)
	return combineRgb((n >> 16) & 255, (n >> 8) & 255, n & 255)
}
const contrast = (hex) => {
	const n = parseInt(String(hex || '#333333').replace('#', ''), 16)
	return 0.299 * ((n >> 16) & 255) + 0.587 * ((n >> 8) & 255) + 0.114 * (n & 255) > 140 ? BLACK : WHITE
}

class DmxLichtInstance extends InstanceBase {
	constructor(internal) {
		super(internal)
		this.state = null // FullState from the app
		this.boot = null // library, palette, effects
		this.live = null // latest live message (bpm, output, timeline)
		this.ws = null
		this.reconnectTimer = null
		this.lastVars = 0
	}

	async init(config) {
		this.config = config
		this.updateStatus(InstanceStatus.Connecting)
		this.defineAll()
		this.connect()
	}

	async destroy() {
		this.closed = true
		clearTimeout(this.reconnectTimer)
		if (this.ws) this.ws.close()
	}

	async configUpdated(config) {
		this.config = config
		// forget the old socket first: its onclose must not schedule a second connection
		const old = this.ws
		this.ws = null
		clearTimeout(this.reconnectTimer)
		if (old) old.close()
		this.connect()
	}

	getConfigFields() {
		return [
			{ type: 'static-text', id: 'info', width: 12, label: 'DMX Light Control', value: 'Address of the PC running DMX Light Control. From another PC: turn on "Control from tablet or phone" in DMX Light Control and fill in the key from its Integrations page.' },
			{ type: 'textinput', id: 'host', label: 'Host', width: 6, default: '127.0.0.1' },
			{ type: 'number', id: 'port', label: 'Port', width: 3, default: 5150, min: 1, max: 65535 },
			{ type: 'textinput', id: 'key', label: 'Key (only needed from another PC)', width: 6, default: '' },
		]
	}

	base() {
		return `http://${this.config.host || '127.0.0.1'}:${this.config.port || 5150}`
	}

	// ---------- talking to the app ----------

	async command(line) {
		const path = String(line).trim().split(/\s+/).map(encodeURIComponent).join('/')
		const key = this.config.key ? `?key=${encodeURIComponent(this.config.key)}` : ''
		try {
			const res = await fetch(`${this.base()}/api/cmd/${path}${key}`)
			if (!res.ok) this.log('warn', `${line}: ${await res.text()}`)
		} catch (e) {
			this.log('error', `${line}: ${e.message}`)
		}
	}

	async connect() {
		clearTimeout(this.reconnectTimer)
		if (this.closed) return
		try {
			const k = this.config.key ? `?key=${encodeURIComponent(this.config.key)}` : ''
			this.boot = await (await fetch(`${this.base()}/api/bootstrap${k}`)).json()
			this.state = await (await fetch(`${this.base()}/api/state${k}`)).json()
			this.defineAll()
			this.updateVariables(true)
		} catch (e) {
			this.updateStatus(InstanceStatus.ConnectionFailure, e.message)
			this.reconnectTimer = setTimeout(() => this.connect(), 3000)
			return
		}
		const url = this.base().replace('http', 'ws') + '/ws' + (this.config.key ? `?key=${encodeURIComponent(this.config.key)}` : '')
		const ws = new WebSocket(url)
		this.ws = ws
		ws.onopen = () => this.updateStatus(InstanceStatus.Ok)
		ws.onmessage = (ev) => {
			let msg
			try { msg = JSON.parse(ev.data) } catch { return }
			if (msg.type === 'state') {
				// actions list scenes, groups, timelines and own chases: redefine when one of those changes
				const shape = (st) => JSON.stringify([
					st?.scenes?.map((s) => [s.id, s.name, s.color]),
					st?.state?.chases?.map((c) => c.name),
					st?.groups?.map((g) => g.name),
					st?.timelines?.map((t) => t.name),
				])
				const before = shape(this.state)
				this.state = msg.state
				if (shape(this.state) !== before) this.defineAll()
				this.checkFeedbacks()
				this.updateVariables(true)
			} else if (msg.type === 'live') {
				const wasConnected = this.live?.output?.connected
				const wasPlaying = this.live?.timeline?.playing
				this.live = msg
				if (wasConnected !== msg.output?.connected) this.checkFeedbacks('dongle_connected')
				if (wasPlaying !== msg.timeline?.playing) this.checkFeedbacks('timeline_playing', 'timeline_waiting')
				this.updateVariables(false)
			}
		}
		ws.onclose = () => {
			if (this.closed || this.ws !== ws) return
			this.updateStatus(InstanceStatus.Disconnected)
			this.reconnectTimer = setTimeout(() => this.connect(), 2000)
		}
		ws.onerror = () => {}
	}

	// ---------- variables (live data is sent 20x/s; 4x/s is plenty here) ----------

	updateVariables(force) {
		const now = Date.now()
		if (!force && now - this.lastVars < 250) return
		this.lastVars = now
		const s = this.state?.state
		const scene = this.state?.scenes?.find((x) => x.id === s?.activeScene)
		const pos = this.live?.timeline?.positionMs ?? 0
		this.setVariableValues({
			bpm: this.live?.music?.bpm != null ? Math.round(this.live.music.bpm) : '',
			active_scene: scene ? scene.name : '',
			master: s ? s.brightnessPercent : '',
			blackout: s?.blackout ? 'on' : 'off',
			mode: s?.mode ?? '',
			output: this.live?.output ? (this.live.output.connected ? 'connected' : 'disconnected') : '',
			timeline_position: `${Math.floor(pos / 60000)}:${String(Math.floor((pos / 1000) % 60)).padStart(2, '0')}`,
			timeline_waiting: this.live?.timeline?.waiting ?? '',
		})
	}

	// ---------- actions, feedbacks, variables, presets ----------

	defineAll() {
		const scenes = (this.state?.scenes ?? []).map((s) => ({ id: s.id, label: s.name }))
		const effects = [
			...(this.boot?.effects ?? []).map((e) => ({ id: e.key, label: e.name })),
			...(this.state?.state?.chases ?? []).map((c) => ({ id: c.name, label: `Own chase: ${c.name}` })),
			{ id: 'screen', label: 'Follow the screen (ambilight)' },
		]
		const colors = [...(this.boot?.palette?.fan ?? []), ...(this.boot?.palette?.specials ?? [])].map((c) => ({ id: c.key, label: c.name }))
		const groups = [{ id: '', label: '(all fixtures)' }, ...(this.state?.groups ?? []).map((g) => ({ id: g.name, label: g.name }))]
		const timelines = (this.state?.timelines ?? []).map((t) => ({ id: t.name, label: t.name }))
		const q = (s) => (String(s).includes(' ') ? `"${s}"` : s)
		const sceneName = (id) => this.state?.scenes?.find((s) => s.id === id)?.name ?? id

		this.setActionDefinitions({
			scene: {
				name: 'Recall scene',
				options: [{ type: 'dropdown', id: 'scene', label: 'Scene', choices: scenes, default: scenes[0]?.id ?? '' }],
				callback: (a) => this.command(`scene ${q(sceneName(a.options.scene))}`),
			},
			blackout: {
				name: 'Blackout',
				options: [{ type: 'dropdown', id: 'mode', label: 'Mode', default: 'toggle', choices: [{ id: 'toggle', label: 'Toggle' }, { id: 'on', label: 'On' }, { id: 'off', label: 'Off' }] }],
				callback: (a) => this.command(`blackout ${a.options.mode}`),
			},
			master: {
				name: 'Master brightness',
				options: [{ type: 'number', id: 'value', label: 'Percent', default: 100, min: 0, max: 100 }],
				callback: (a) => this.command(`master ${a.options.value}`),
			},
			master_step: {
				name: 'Master brightness up/down',
				options: [{ type: 'number', id: 'step', label: 'Step (+/-)', default: 10, min: -100, max: 100 }],
				callback: (a) => this.command(`master ${Math.max(0, Math.min(100, (this.state?.state?.brightnessPercent ?? 100) + a.options.step))}`),
			},
			on: {
				name: 'Fixtures on',
				options: [{ type: 'dropdown', id: 'group', label: 'Group', choices: groups, default: '' }],
				callback: (a) => this.command(`on ${q(a.options.group)}`.trim()),
			},
			off: {
				name: 'Fixtures off',
				options: [{ type: 'dropdown', id: 'group', label: 'Group', choices: groups, default: '' }],
				callback: (a) => this.command(`off ${q(a.options.group)}`.trim()),
			},
			color: {
				name: 'Colour',
				options: [
					{ type: 'dropdown', id: 'color', label: 'Colour', choices: colors, default: 'red', allowCustom: true },
					{ type: 'dropdown', id: 'group', label: 'Group', choices: groups, default: '' },
				],
				callback: (a) => this.command(`color ${a.options.color} ${q(a.options.group)}`.trim()),
			},
			effect: {
				name: 'Start effect',
				options: [
					{ type: 'dropdown', id: 'effect', label: 'Effect', choices: effects, default: effects[0]?.id ?? 'blink' },
					{ type: 'dropdown', id: 'group', label: 'Group', choices: groups, default: '' },
				],
				callback: (a) => this.command(`effect ${q(a.options.effect)} ${q(a.options.group)}`.trim()),
			},
			stop: { name: 'Stop effects', options: [], callback: () => this.command('stop') },
			flash: {
				name: 'Flash a colour (notification)',
				options: [
					{ type: 'dropdown', id: 'color', label: 'Colour', choices: colors, default: 'green', allowCustom: true },
					{ type: 'number', id: 'seconds', label: 'Seconds', default: 5, min: 0.2, max: 600 },
					{ type: 'checkbox', id: 'solid', label: 'Solid (instead of blinking)', default: false },
				],
				callback: (a) => this.command(`flash ${a.options.color} ${a.options.seconds}${a.options.solid ? ' solid' : ''}`),
			},
			tap: { name: 'Tap tempo', options: [], callback: () => this.command('tap') },
			bpm: {
				name: 'Set BPM',
				options: [{ type: 'number', id: 'bpm', label: 'BPM', default: 120, min: 30, max: 300 }],
				callback: (a) => this.command(`bpm ${a.options.bpm}`),
			},
			strobe: {
				name: 'Strobe (use "on" on press and "off" on release)',
				options: [{ type: 'dropdown', id: 'mode', label: 'Mode', default: 'on', choices: [{ id: 'on', label: 'On' }, { id: 'off', label: 'Off' }, { id: 'toggle', label: 'Toggle' }] }],
				callback: (a) => this.command(`strobe ${a.options.mode}`),
			},
			fade: {
				name: 'Transition time',
				options: [{ type: 'number', id: 'seconds', label: 'Seconds', default: 1, min: 0, max: 60 }],
				callback: (a) => this.command(`fade ${a.options.seconds}`),
			},
			timeline: {
				name: 'Timeline',
				options: [
					{ type: 'dropdown', id: 'verb', label: 'Action', default: 'play', choices: [
						{ id: 'play', label: 'Play / GO' }, { id: 'toggle', label: 'Play/Pause' }, { id: 'pause', label: 'Pause' }, { id: 'stop', label: 'Stop' },
						{ id: 'next', label: 'Next point' }, { id: 'prev', label: 'Previous point' }] },
					{ type: 'dropdown', id: 'name', label: 'Timeline', choices: timelines, default: timelines[0]?.id ?? '' },
				],
				callback: (a) => this.command(['next', 'prev', 'toggle'].includes(a.options.verb) ? `timeline ${a.options.verb}` : `timeline ${a.options.verb} ${q(a.options.name)}`.trim()),
			},
			custom: {
				name: 'Custom command',
				options: [{ type: 'textinput', id: 'line', label: 'Command (e.g. "scene 2", "flash red 3 solid")', default: 'scene 1' }],
				callback: (a) => this.command(a.options.line),
			},
		})

		this.setFeedbackDefinitions({
			scene_active: {
				type: 'boolean',
				name: 'Scene is active',
				defaultStyle: { bgcolor: ACCENT, color: BLACK },
				options: [{ type: 'dropdown', id: 'scene', label: 'Scene', choices: scenes, default: scenes[0]?.id ?? '' }],
				callback: (f) => this.state?.state?.activeScene === f.options.scene,
			},
			blackout_on: {
				type: 'boolean',
				name: 'Blackout is on',
				defaultStyle: { bgcolor: RED, color: WHITE },
				options: [],
				callback: () => !!this.state?.state?.blackout,
			},
			dongle_connected: {
				type: 'boolean',
				name: 'Output connected',
				defaultStyle: { bgcolor: GREEN, color: WHITE },
				options: [],
				callback: () => !!this.live?.output?.connected,
			},
			timeline_waiting: {
				type: 'boolean',
				name: 'Timeline waits for GO',
				defaultStyle: { bgcolor: ACCENT, color: BLACK },
				options: [],
				callback: () => this.live?.timeline?.waiting != null,
			},
			timeline_playing: {
				type: 'boolean',
				name: 'Timeline playing',
				defaultStyle: { bgcolor: GREEN, color: WHITE },
				options: [],
				callback: () => !!this.live?.timeline?.playing,
			},
		})

		this.setVariableDefinitions([
			{ variableId: 'bpm', name: 'Tempo (BPM)' },
			{ variableId: 'active_scene', name: 'Active scene' },
			{ variableId: 'master', name: 'Master brightness (%)' },
			{ variableId: 'blackout', name: 'Blackout (on/off)' },
			{ variableId: 'mode', name: 'Mode (static/effect/faders)' },
			{ variableId: 'output', name: 'Output status' },
			{ variableId: 'timeline_position', name: 'Timeline position (m:ss)' },
			{ variableId: 'timeline_waiting', name: 'Wait point the timeline is waiting at' },
		])

		const presets = {}
		for (const s of this.state?.scenes ?? []) {
			presets[`scene_${s.id}`] = {
				type: 'button',
				category: 'Scenes',
				name: s.name,
				style: { text: s.name, size: 'auto', color: contrast(s.color), bgcolor: hexToRgb(s.color) },
				steps: [{ down: [{ actionId: 'scene', options: { scene: s.id } }], up: [] }],
				feedbacks: [{ feedbackId: 'scene_active', options: { scene: s.id }, style: { bgcolor: ACCENT, color: BLACK } }],
			}
		}
		presets.blackout = {
			type: 'button', category: 'Control', name: 'Blackout',
			style: { text: 'BLACK\\nOUT', size: 'auto', color: WHITE, bgcolor: BLACK },
			steps: [{ down: [{ actionId: 'blackout', options: { mode: 'toggle' } }], up: [] }],
			feedbacks: [{ feedbackId: 'blackout_on', options: {}, style: { bgcolor: RED, color: WHITE } }],
		}
		presets.tap = {
			type: 'button', category: 'Control', name: 'Tap tempo',
			style: { text: 'TAP\\n$(dmxlicht:bpm)', size: 'auto', color: BLACK, bgcolor: ACCENT },
			steps: [{ down: [{ actionId: 'tap', options: {} }], up: [] }], feedbacks: [],
		}
		presets.strobe = {
			type: 'button', category: 'Control', name: 'Strobe (hold)',
			style: { text: 'STROBE', size: 'auto', color: BLACK, bgcolor: WHITE },
			steps: [{ down: [{ actionId: 'strobe', options: { mode: 'on' } }], up: [{ actionId: 'strobe', options: { mode: 'off' } }] }], feedbacks: [],
		}
		presets.stop = {
			type: 'button', category: 'Control', name: 'Stop effects',
			style: { text: 'STOP\\nFX', size: 'auto', color: WHITE, bgcolor: combineRgb(80, 20, 20) },
			steps: [{ down: [{ actionId: 'stop', options: {} }], up: [] }], feedbacks: [],
		}
		for (const [c, name] of [['green', 'Green'], ['red', 'Red'], ['blue', 'Blue'], ['white', 'White']]) {
			presets[`flash_${c}`] = {
				type: 'button', category: 'Flash', name: `Flash ${name}`,
				style: { text: `FLASH\\n${name.toUpperCase()}`, size: 'auto', color: c === 'white' ? BLACK : WHITE, bgcolor: hexToRgb({ green: '#1ea046', red: '#c81e1e', blue: '#1e46c8', white: '#ffffff' }[c]) },
				steps: [{ down: [{ actionId: 'flash', options: { color: c, seconds: 5, solid: false } }], up: [] }], feedbacks: [],
			}
		}
		presets.timeline_go = {
			type: 'button', category: 'Timeline', name: 'GO / Play',
			style: { text: 'GO\\n$(dmxlicht:timeline_waiting)', size: 'auto', color: WHITE, bgcolor: GREEN },
			steps: [{ down: [{ actionId: 'timeline', options: { verb: 'play', name: '' } }], up: [] }],
			feedbacks: [{ feedbackId: 'timeline_waiting', options: {}, style: { bgcolor: ACCENT, color: BLACK } }],
		}
		presets.timeline_stop = {
			type: 'button', category: 'Timeline', name: 'Stop',
			style: { text: 'STOP', size: 'auto', color: WHITE, bgcolor: RED },
			steps: [{ down: [{ actionId: 'timeline', options: { verb: 'stop', name: '' } }], up: [] }], feedbacks: [],
		}
		presets.timeline_prev = {
			type: 'button', category: 'Timeline', name: 'Previous point',
			style: { text: '|◀', size: '24', color: WHITE, bgcolor: BLACK },
			steps: [{ down: [{ actionId: 'timeline', options: { verb: 'prev', name: '' } }], up: [] }], feedbacks: [],
		}
		presets.timeline_next = {
			type: 'button', category: 'Timeline', name: 'Next point',
			style: { text: '▶|', size: '24', color: WHITE, bgcolor: BLACK },
			steps: [{ down: [{ actionId: 'timeline', options: { verb: 'next', name: '' } }], up: [] }], feedbacks: [],
		}
		presets.usb_recover = {
			type: 'button', category: 'Control', name: 'Recover USB',
			style: { text: 'USB\\nHERSTEL', size: 'auto', color: WHITE, bgcolor: combineRgb(40, 40, 90) },
			steps: [{ down: [{ actionId: 'custom', options: { line: 'recover' } }], up: [] }],
			feedbacks: [{ feedbackId: 'dongle_connected', options: {}, style: { bgcolor: GREEN, color: WHITE } }],
		}
		presets.output = {
			type: 'button', category: 'Control', name: 'Output status',
			style: { text: 'DMX\\n$(dmxlicht:output)', size: '7', color: WHITE, bgcolor: RED },
			steps: [{ down: [], up: [] }],
			feedbacks: [{ feedbackId: 'dongle_connected', options: {}, style: { bgcolor: GREEN, color: WHITE } }],
		}
		this.setPresetDefinitions(presets)
	}
}

runEntrypoint(DmxLichtInstance, [])
