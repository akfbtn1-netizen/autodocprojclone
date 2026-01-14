/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      // Distinctive enterprise palette - warm neutrals with a refined teal accent
      colors: {
        // Primary brand - sophisticated teal
        brand: {
          50: '#f0fdfa',
          100: '#ccfbf1',
          200: '#99f6e4',
          300: '#5eead4',
          400: '#2dd4bf',
          500: '#14b8a6',
          600: '#0d9488',
          700: '#0f766e',
          800: '#115e59',
          900: '#134e4a',
          950: '#042f2e',
        },
        // Warm slate for backgrounds - not cold gray
        surface: {
          50: '#fafaf9',
          100: '#f5f5f4',
          200: '#e7e5e4',
          300: '#d6d3d1',
          400: '#a8a29e',
          500: '#78716c',
          600: '#57534e',
          700: '#44403c',
          800: '#292524',
          900: '#1c1917',
          950: '#0c0a09',
        },
        // Status colors - refined and professional
        status: {
          draft: {
            bg: '#fef9c3',
            border: '#eab308',
            text: '#854d0e',
          },
          pending: {
            bg: '#ffedd5',
            border: '#f97316',
            text: '#9a3412',
          },
          review: {
            bg: '#dbeafe',
            border: '#3b82f6',
            text: '#1e40af',
          },
          approved: {
            bg: '#dcfce7',
            border: '#22c55e',
            text: '#166534',
          },
          completed: {
            bg: '#f3e8ff',
            border: '#a855f7',
            text: '#6b21a8',
          },
          rejected: {
            bg: '#fee2e2',
            border: '#ef4444',
            text: '#991b1b',
          },
        },
      },
      fontFamily: {
        // Distinctive typography pairing
        sans: ['Plus Jakarta Sans', 'system-ui', 'sans-serif'],
        display: ['Outfit', 'system-ui', 'sans-serif'],
        mono: ['JetBrains Mono', 'Consolas', 'monospace'],
      },
      fontSize: {
        '2xs': ['0.625rem', { lineHeight: '0.875rem' }],
      },
      boxShadow: {
        'glow': '0 0 20px -5px rgba(20, 184, 166, 0.3)',
        'glow-lg': '0 0 40px -10px rgba(20, 184, 166, 0.4)',
        'card': '0 1px 3px 0 rgb(0 0 0 / 0.04), 0 1px 2px -1px rgb(0 0 0 / 0.04)',
        'card-hover': '0 10px 25px -5px rgb(0 0 0 / 0.08), 0 4px 10px -6px rgb(0 0 0 / 0.04)',
        'elevated': '0 20px 40px -15px rgb(0 0 0 / 0.1)',
      },
      animation: {
        'pulse-slow': 'pulse 3s cubic-bezier(0.4, 0, 0.6, 1) infinite',
        'glow-pulse': 'glow-pulse 2s ease-in-out infinite',
        'slide-up': 'slide-up 0.4s ease-out',
        'slide-down': 'slide-down 0.4s ease-out',
        'fade-in': 'fade-in 0.3s ease-out',
        'scale-in': 'scale-in 0.2s ease-out',
        'shimmer': 'shimmer 2s linear infinite',
      },
      keyframes: {
        'glow-pulse': {
          '0%, 100%': { boxShadow: '0 0 20px -5px rgba(20, 184, 166, 0.2)' },
          '50%': { boxShadow: '0 0 30px -5px rgba(20, 184, 166, 0.5)' },
        },
        'slide-up': {
          '0%': { opacity: '0', transform: 'translateY(10px)' },
          '100%': { opacity: '1', transform: 'translateY(0)' },
        },
        'slide-down': {
          '0%': { opacity: '0', transform: 'translateY(-10px)' },
          '100%': { opacity: '1', transform: 'translateY(0)' },
        },
        'fade-in': {
          '0%': { opacity: '0' },
          '100%': { opacity: '1' },
        },
        'scale-in': {
          '0%': { opacity: '0', transform: 'scale(0.95)' },
          '100%': { opacity: '1', transform: 'scale(1)' },
        },
        'shimmer': {
          '0%': { backgroundPosition: '-200% 0' },
          '100%': { backgroundPosition: '200% 0' },
        },
      },
      backgroundImage: {
        'gradient-radial': 'radial-gradient(var(--tw-gradient-stops))',
        'gradient-mesh': 'url("data:image/svg+xml,%3Csvg width=\'60\' height=\'60\' viewBox=\'0 0 60 60\' xmlns=\'http://www.w3.org/2000/svg\'%3E%3Cpath d=\'M54.627 0l.83.828-1.415 1.415L51.8 0h2.827zM5.373 0l-.83.828L5.96 2.243 8.2 0H5.374zM48.97 0l3.657 3.657-1.414 1.414L46.143 0h2.828zM11.03 0L7.372 3.657 8.787 5.07 13.857 0H11.03zm32.284 0L49.8 6.485 48.384 7.9l-7.9-7.9h2.83zM16.686 0L10.2 6.485 11.616 7.9l7.9-7.9h-2.83zM22.344 0L13.858 8.485 15.272 9.9l9.9-9.9h-2.83zM27.03 0L19.544 7.485 20.96 8.9l9.9-9.9h-2.83zM32.688 0L24.2 8.485 25.616 9.9l9.9-9.9h-2.83zM38.344 0L29.858 8.485 31.272 9.9l9.9-9.9h-2.83zM0 5.373l.828-.83L2.243 5.96 0 8.2V5.374zm0 5.656l.828-.83 5.657 5.657-1.414 1.414L0 11.03v-.001zm0 5.656l.828-.828 11.314 11.314-1.414 1.414L0 16.686v-.001zm0 5.657l.828-.828 16.97 16.97-1.414 1.415L0 22.343v-.001zM0 28l.828-.828 22.627 22.627-1.414 1.414L0 28v-.001zm0 5.657l.828-.828 28.284 28.284-1.414 1.414L0 33.657v-.001zm0 5.657l.828-.828 33.94 33.94-1.414 1.415L0 39.314v-.001zm0 5.657l.828-.828 39.598 39.598-1.414 1.414L0 44.97v-.001zM0 60v-4.627l.828.83L45.255 60H0zm0-9.37v-2.828l.828.828L55.6 60h-2.83L0 50.63zm0-5.656V41.8l.828.828L60 60h-3.185L0 44.97zm0-5.656V36.14l.828.83L60 53.628V60h-3.185L0 39.314zm0-5.657v-2.828l.828.828L60 47.97V54.8l-3.185-3.185L0 33.657zm0-5.657v-2.828l.828.828L60 42.314V49.14l-3.185-3.185L0 28zm0-5.657V16.8l.828.828L60 36.657v6.828l-3.185-3.185L0 22.343zm0-5.657V11.14l.828.83L60 31v6.828l-3.185-3.185L0 16.686zM0 5.37v-.57c0-.193.016-.384.047-.57L60 64.83V55.17l-3.185 3.185L0 8.2V5.37z\' fill=\'%2314b8a6\' fill-opacity=\'0.02\' fill-rule=\'evenodd\'/%3E%3C/svg%3E")',
        'noise': 'url("data:image/svg+xml,%3Csvg viewBox=\'0 0 400 400\' xmlns=\'http://www.w3.org/2000/svg\'%3E%3Cfilter id=\'noiseFilter\'%3E%3CfeTurbulence type=\'fractalNoise\' baseFrequency=\'0.9\' numOctaves=\'3\' stitchTiles=\'stitch\'/%3E%3C/filter%3E%3Crect width=\'100%25\' height=\'100%25\' filter=\'url(%23noiseFilter)\'/%3E%3C/svg%3E")',
      },
      borderRadius: {
        '4xl': '2rem',
      },
    },
  },
  plugins: [],
}
