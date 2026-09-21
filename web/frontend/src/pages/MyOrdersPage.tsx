import { OrdersPanel } from '../components/OrdersPanel';

export function MyOrdersPage() {
  return (
    <OrdersPanel
      refreshToken={0}
      title="My orders"
      hint="Only your own orders. The API decides that from your token, not from anything this page asks for."
      showCustomer={false}
    />
  );
}
