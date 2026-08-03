import { createFileRoute } from '@tanstack/react-router'
import { Garden } from '../pages/Garden'

export const Route = createFileRoute('/_authenticated/garden')({
  component: Garden,
})
